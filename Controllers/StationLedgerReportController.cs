using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Reporting.NETCore;
using Nskg.Data;
using Nskg.Extensions;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace Nskg.Controllers
{
    [Authorize]
    public class StationLedgerReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public StationLedgerReportController(ApplicationDbContext context, IConfiguration config, IWebHostEnvironment env)
        {
            _context = context;
            _config = config;
            _env = env;
        }

        private int GetCompanyId(int? companyId = null)
        {
            if (companyId.HasValue) return companyId.Value;
            var compId = User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(compId, out int id) && id > 0) return id;
            return 1006;
        }

        private int GetFinancialYearId()
        {
            var fyId = User.FindFirst("FinancialYearId")?.Value;
            if (int.TryParse(fyId, out int id) && id > 0) return id;
            return 4;
        }

        [HttpGet]
        public IActionResult GetAccountsByCompany(int companyId)
        {
            var query = _context.GLChart3
                .Where(x => (companyId == 0 || x.CompanyId == companyId || x.CompanyId == 0 || x.CompanyId == null) 
                         && (x.AC1 == "067" || x.AC1 == "068") 
                         && !string.IsNullOrEmpty(x.Name));

            var accounts = query
                .Select(x => new
                {
                    Code = (x.ACC ?? (x.AC1 + x.AC3)).Trim(),
                    Name = x.Name.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.Code))
                .ToList()
                .GroupBy(x => x.Code)
                .Select(g => new
                {
                    code = g.Key,
                    name = g.First().Name
                })
                .OrderBy(x => x.name)
                .ToList();

            return Json(accounts);
        }

        [HttpGet]
        public IActionResult Index(string? accode, string? fromDate, string? toDate, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);

            var companies = _context.Companies
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Cocode)
                .Select(c => new
                {
                    Id = c.Id,
                    Code = c.Cocode,
                    Name = (!string.IsNullOrEmpty(c.Cocode) ? c.Cocode + " - " : "") + c.Name
                })
                .ToList();

            var query = _context.GLChart3
                .Where(x => (selectedCompanyId == 0 || x.CompanyId == selectedCompanyId || x.CompanyId == 0 || x.CompanyId == null) 
                         && (x.AC1 == "067" || x.AC1 == "068") 
                         && !string.IsNullOrEmpty(x.Name));

            var accounts = query
                .Select(x => new
                {
                    Code = (x.ACC ?? (x.AC1 + x.AC3)).Trim(),
                    Name = x.Name.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.Code))
                .ToList()
                .GroupBy(x => x.Code)
                .Select(g => new
                {
                    Code = g.Key,
                    Name = g.First().Name
                })
                .OrderBy(x => x.Name)
                .ToList();

            ViewBag.CompanyList = companies;
            ViewBag.SelectedCompanyId = selectedCompanyId;
            ViewBag.AccountList = accounts;
            ViewBag.Accode = accode;
            ViewBag.FromDate = string.IsNullOrEmpty(fromDate) ? DateTime.Now.ToString("yyyy-07-01") : fromDate;
            ViewBag.ToDate = string.IsNullOrEmpty(toDate) ? DateTime.Now.ToString("yyyy-MM-dd") : toDate;

            return View();
        }

        private DataTable GetStationLedger(string accode, DateTime fromDate, DateTime toDate, int companyId, int financialYearId)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                // 1. Run dbo.PROCESSDETAIL_Station to populate ACCUMULATED & ACCOPEN
                try
                {
                    if (companyId > 0)
                    {
                        using (SqlCommand cmdProc = new SqlCommand("dbo.PROCESSDETAIL_Station", con))
                        {
                            cmdProc.CommandType = CommandType.StoredProcedure;
                            cmdProc.CommandTimeout = 180;
                            cmdProc.Parameters.AddWithValue("@CompanyId", companyId);
                            cmdProc.Parameters.AddWithValue("@TDATE", toDate.Date);
                            cmdProc.Parameters.AddWithValue("@ACCODE", accode?.Trim() ?? "");
                            cmdProc.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        var activeCompanies = _context.Companies.Where(c => !c.IsDeleted).Select(c => c.Id).ToList();
                        if (activeCompanies.Count > 0)
                        {
                            using (SqlCommand cmdSetup = new SqlCommand(@"
                                IF OBJECT_ID('tempdb..#AllAcc') IS NOT NULL DROP TABLE #AllAcc;
                                SELECT TOP 0 * INTO #AllAcc FROM ACCUMULATED;", con))
                            {
                                cmdSetup.ExecuteNonQuery();
                            }

                            foreach (var cId in activeCompanies)
                            {
                                using (SqlCommand cmdProc = new SqlCommand("dbo.PROCESSDETAIL_Station", con))
                                {
                                    cmdProc.CommandType = CommandType.StoredProcedure;
                                    cmdProc.CommandTimeout = 180;
                                    cmdProc.Parameters.AddWithValue("@CompanyId", cId);
                                    cmdProc.Parameters.AddWithValue("@TDATE", toDate.Date);
                                    cmdProc.Parameters.AddWithValue("@ACCODE", accode?.Trim() ?? "");
                                    cmdProc.ExecuteNonQuery();
                                }

                                using (SqlCommand cmdCopy = new SqlCommand("INSERT INTO #AllAcc SELECT * FROM ACCUMULATED;", con))
                                {
                                    cmdCopy.ExecuteNonQuery();
                                }
                            }

                            using (SqlCommand cmdMerge = new SqlCommand(@"
                                DELETE FROM ACCUMULATED;

                                ;WITH DistinctAcc AS
                                (
                                    SELECT *,
                                           ROW_NUMBER() OVER(
                                               PARTITION BY VONO, VOTYPE, CAST(VODATE AS DATE), DRAMT, CRAMT, ISNULL(BILLTINO, 0), ISNULL(BILNO, 0), ISNULL(VEHICLENO, ''), ISNULL(NARRATION, '')
                                               ORDER BY COCODE
                                           ) AS rn
                                    FROM #AllAcc
                                )
                                INSERT INTO ACCUMULATED (
                                    COCODE, VONO, VODATE, VOTYPE, AC1, AC2, AC3, ACTYPE, DRAMT, CRAMT, NARRATION,
                                    CANCEL, PAY, INVNO, INVDATE, ACC, NDRAMT, NCRAMT, RATE, AMOUNT, CHQNO,
                                    CHQDATE, STAXAMT, DUEDATE, INAME, QTY, BILLTINO, BILNO, DELIVERYAMT, DELIVERYAMT2,
                                    STATION, VEHICLENO
                                )
                                SELECT 
                                    COCODE, VONO, VODATE, VOTYPE, AC1, AC2, AC3, ACTYPE, DRAMT, CRAMT, NARRATION,
                                    CANCEL, PAY, INVNO, INVDATE, ACC, NDRAMT, NCRAMT, RATE, AMOUNT, CHQNO,
                                    CHQDATE, STAXAMT, DUEDATE, INAME, QTY, BILLTINO, BILNO, DELIVERYAMT, DELIVERYAMT2,
                                    STATION, VEHICLENO
                                FROM DistinctAcc
                                WHERE rn = 1;

                                DROP TABLE #AllAcc;", con))
                            {
                                cmdMerge.ExecuteNonQuery();
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback
                }

                // 2. Fetch Account Name & Company Name
                string accName = "";
                using (SqlCommand cmdAcc = new SqlCommand(
                    "SELECT TOP 1 Name FROM GLCHART3 WHERE (@CompanyId = 0 OR CompanyId = @CompanyId OR CompanyId IS NULL) AND (RTRIM(AC1) + RTRIM(AC3) = RTRIM(@Accode) OR ACC = RTRIM(@Accode))", con))
                {
                    cmdAcc.Parameters.AddWithValue("@CompanyId", companyId);
                    cmdAcc.Parameters.AddWithValue("@Accode", accode?.Trim() ?? "");
                    var res = cmdAcc.ExecuteScalar();
                    if (res != null && res != DBNull.Value) accName = res.ToString();
                }

                string companyName = "West Wharf-New Shadab Karachi Goods Transports";
                if (companyId > 0)
                {
                    using (SqlCommand cmdComp = new SqlCommand("SELECT TOP 1 Name FROM Companies WHERE Id = @CompanyId", con))
                    {
                        cmdComp.Parameters.AddWithValue("@CompanyId", companyId);
                        var res = cmdComp.ExecuteScalar();
                        if (res != null && res != DBNull.Value && !string.IsNullOrWhiteSpace(res.ToString()))
                            companyName = res.ToString();
                    }
                }
                else
                {
                    companyName = "New Shadab Karachi Goods Transport Company (All Branches - Linked)";
                }

                // 3. Query Opening Balance and Transactions with Running Balance
                string query = @"
                    DECLARE @AnnualOpeningBal DECIMAL(18,2) = 0;

                    IF OBJECT_ID('OpeningBalances', 'U') IS NOT NULL
                    BEGIN
                        SELECT @AnnualOpeningBal = ISNULL(SUM(Debit - Credit), 0)
                        FROM OpeningBalances
                        WHERE Accode = @Accode
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId)
                          AND FinancialYearId = @FinancialYearId;
                    END

                    DECLARE @OpeningBal DECIMAL(18,2) = 0;

                    SELECT @OpeningBal = @AnnualOpeningBal + ISNULL(SUM(ISNULL(DRAMT, 0) - ISNULL(CRAMT, 0)), 0)
                    FROM ACCUMULATED
                    WHERE VODATE < @FromDate;

                    ;WITH RawData AS
                    (
                        -- Opening Balance Row
                        SELECT 
                            0 AS SortOrder,
                            CAST(NULL AS DATE) AS DocDate,
                            CAST('' AS VARCHAR(50)) AS DocNo,
                            CAST('' AS VARCHAR(10)) AS Votype,
                            CAST(NULL AS VARCHAR(50)) AS Comm,
                            CAST('Opening Balance' AS VARCHAR(100)) AS VehicleNo,
                            CAST('' AS VARCHAR(100)) AS Station,
                            CAST('' AS VARCHAR(250)) AS Narration,
                            CAST('' AS VARCHAR(50)) AS CompanyCode,
                            CAST(NULL AS DECIMAL(18,2)) AS DeliveryAmt,
                            CAST(NULL AS DECIMAL(18,2)) AS DeliveryAmt2,
                            CASE WHEN @OpeningBal > 0 THEN @OpeningBal ELSE CAST(0 AS DECIMAL(18,2)) END AS Debit,
                            CASE WHEN @OpeningBal < 0 THEN ABS(@OpeningBal) ELSE CAST(0 AS DECIMAL(18,2)) END AS Credit,
                            @OpeningBal AS Balance,
                            CAST(0 AS BIGINT) AS RowNum

                        UNION ALL

                        -- Transactions between FromDate and ToDate
                        SELECT 
                            1 AS SortOrder,
                            CAST(a.VODATE AS DATE) AS DocDate,
                            CASE 
                                WHEN CHARINDEX('/', a.VONO) > 0 THEN LEFT(a.VONO, CHARINDEX('/', a.VONO) - 1)
                                ELSE ISNULL(a.VONO, '')
                            END AS DocNo,
                            ISNULL(a.VOTYPE, '') AS Votype,
                            CASE WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN CAST(CAST(a.BILLTINO AS BIGINT) AS VARCHAR(50)) ELSE NULL END AS Comm,
                            CASE 
                                WHEN NULLIF(RTRIM(LTRIM(a.VEHICLENO)), '') IS NOT NULL THEN RTRIM(LTRIM(a.VEHICLENO))
                                ELSE COALESCE(
                                    (SELECT TOP 1 vd.Vehicleno FROM VoDet vd WHERE vd.Vono = a.VONO AND vd.Votype = a.VOTYPE AND NULLIF(RTRIM(LTRIM(vd.Vehicleno)), '') IS NOT NULL),
                                    (SELECT TOP 1 ish.VehicleNo FROM ISSHEAD ish WHERE (ish.BillTiNo = a.BILLTINO OR ish.BilNo = a.BILNO) AND NULLIF(RTRIM(LTRIM(ish.VehicleNo)), '') IS NOT NULL),
                                    (SELECT TOP 1 ch.VehicleNo FROM ChallanDet cd INNER JOIN ChallanHead ch ON cd.ChallanHeadId = ch.Id WHERE (cd.BillTiNo = a.BILLTINO OR cd.BilNo = a.BILNO) AND NULLIF(RTRIM(LTRIM(ch.VehicleNo)), '') IS NOT NULL),
                                    (SELECT TOP 1 cm.VehicleNo FROM CommDetail cmd INNER JOIN CommHead cm ON cmd.CommHeadId = cm.Id WHERE cmd.BillTiNo = a.BILLTINO AND NULLIF(RTRIM(LTRIM(cm.VehicleNo)), '') IS NOT NULL),
                                    (SELECT TOP 1 ch.VehicleNo FROM ChallanHead ch WHERE ch.ChalNo = a.BILLTINO AND NULLIF(RTRIM(LTRIM(ch.VehicleNo)), '') IS NOT NULL),
                                    (SELECT TOP 1 cm.VehicleNo FROM CommHead cm WHERE cm.DocNo = a.VONO AND NULLIF(RTRIM(LTRIM(cm.VehicleNo)), '') IS NOT NULL),
                                    CASE WHEN a.VOTYPE = 'CL' THEN NULLIF(RTRIM(LTRIM(a.NARRATION)), '') ELSE NULL END,
                                    ''
                                )
                            END AS VehicleNo,
                            ISNULL(a.INAME, '') AS Station,
                            ISNULL(a.NARRATION, '') AS Narration,
                            CASE 
                                WHEN RTRIM(LTRIM(ISNULL(a.COCODE, ''))) IN ('01', '1006', '1') THEN 'W.W'
                                WHEN RTRIM(LTRIM(ISNULL(a.COCODE, ''))) IN ('02', '1007', '2') THEN 'M.P'
                                WHEN RTRIM(LTRIM(ISNULL(a.COCODE, ''))) IN ('03', '1008', '3') THEN 'N.K'
                                WHEN RTRIM(LTRIM(ISNULL(a.COCODE, ''))) IN ('04', '1009', '4') THEN 'R.W'
                                ELSE 'W.W'
                            END AS CompanyCode,
                            a.DELIVERYAMT AS DeliveryAmt,
                            a.DELIVERYAMT2 AS DeliveryAmt2,
                            ISNULL(a.DRAMT, 0) AS Debit,
                            ISNULL(a.CRAMT, 0) AS Credit,
                            @OpeningBal + SUM(ISNULL(a.DRAMT, 0) - ISNULL(a.CRAMT, 0)) OVER (
                                ORDER BY a.VODATE, a.VONO, (SELECT NULL)
                                ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                            ) AS Balance,
                            ROW_NUMBER() OVER (ORDER BY a.VODATE, a.VONO) AS RowNum
                        FROM ACCUMULATED a
                        WHERE a.VODATE >= @FromDate AND a.VODATE <= @ToDate
                    )
                    SELECT 
                        SortOrder,
                        DocDate,
                        DocNo,
                        Votype,
                        Comm,
                        VehicleNo,
                        Station,
                        Narration,
                        CompanyCode,
                        DeliveryAmt,
                        DeliveryAmt2,
                        Debit,
                        Credit,
                        Balance,
                        @Accode AS Accode,
                        @AccName AS AccName,
                        @CompanyName AS CompanyName,
                        @FromDate AS FromDate,
                        @ToDate AS ToDate
                    FROM RawData
                    ORDER BY SortOrder, DocDate, DocNo, RowNum;";

                using (SqlCommand cmdData = new SqlCommand(query, con))
                {
                    cmdData.CommandTimeout = 180;
                    cmdData.Parameters.AddWithValue("@FromDate", fromDate.Date);
                    cmdData.Parameters.AddWithValue("@ToDate", toDate.Date);
                    cmdData.Parameters.AddWithValue("@Accode", accode?.Trim() ?? "");
                    cmdData.Parameters.AddWithValue("@AccName", accName);
                    cmdData.Parameters.AddWithValue("@CompanyName", companyName);
                    cmdData.Parameters.AddWithValue("@CompanyId", companyId);
                    cmdData.Parameters.AddWithValue("@FinancialYearId", financialYearId);

                    using (SqlDataReader reader = cmdData.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult GeneratePDF(string? accode, DateTime fromDate, DateTime toDate, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetStationLedger(accode ?? "", fromDate, toDate, selectedCompanyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found for selected date range and station account.");
                }

                string reportPath = Path.Combine(_env.WebRootPath, "Reports", "StationLedgerrpt.rdlc");

                if (!System.IO.File.Exists(reportPath))
                {
                    return Content($"Report file not found: {reportPath}");
                }

                using (LocalReport report = new LocalReport())
                {
                    report.ReportPath = reportPath;
                    report.DataSources.Clear();

                    dt.TableName = "DSStationLedger";
                    report.DataSources.Add(new ReportDataSource("DSStationLedger", dt));

                    byte[] pdfBytes = report.Render("PDF");

                    if (pdfBytes == null || pdfBytes.Length < 100)
                    {
                        return Content("PDF generation failed or corrupted output.");
                    }

                    return File(pdfBytes, "application/pdf", $"StationLedger_{accode}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf", enableRangeProcessing: true);
                }
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult OnScreenReport(string? accode, DateTime fromDate, DateTime toDate, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetStationLedger(accode ?? "", fromDate, toDate, selectedCompanyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No data found for selected date range and station account.</strong></div>", "text/html");
                }

                string compName = (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "West Wharf-New Shadab Karachi Goods Transports";
                string accName = (dt.Rows.Count > 0 ? dt.Rows[0]["AccName"]?.ToString() : null) ?? "";
                string formattedAccode = accode ?? "";

                decimal totalDebit = 0, totalCredit = 0;
                var sb = new StringBuilder();
                sb.Append(@"<!DOCTYPE html><html><head><meta charset='utf-8'>
                <title>Station Ledger - " + formattedAccode + @"</title>
                <style>
                    body { font-family: Arial, sans-serif; font-size: 11px; margin: 15px; color: #000; }
                    .header-box { text-align: center; margin-bottom: 12px; }
                    .comp-name { font-size: 16px; font-weight: bold; margin-bottom: 3px; letter-spacing: 0.5px; }
                    .report-title { font-size: 13px; font-weight: bold; letter-spacing: 3px; margin-bottom: 8px; }
                    .meta-row { display: flex; justify-content: space-between; font-size: 11px; margin-bottom: 8px; }
                    .acc-prominent { font-size: 14px; font-weight: bold; text-align: left; margin: 10px 0 6px 0; border-bottom: 1px solid #000; padding-bottom: 3px; }
                    .acc-code { display: inline-block; width: 100px; font-weight: bold; }
                    .acc-name { font-weight: bold; }
                    table { width: 100%; border-collapse: collapse; margin-top: 4px; }
                    th { border-top: 1px solid #000; border-bottom: 1px solid #000; padding: 4px 5px; font-size: 10.5px; font-weight: bold; text-align: left; }
                    td { padding: 4px 5px; border-bottom: 1px solid #e0e0e0; font-size: 10.5px; }
                    .num { text-align: right; }
                    .center { text-align: center; }
                    .bold { font-weight: bold; }
                    .op-row td { font-weight: bold; background-color: #fafafa; }
                    tfoot tr td { border-top: 1px solid #000; border-bottom: 2px solid #000; font-weight: bold; padding: 6px; }
                    @media print {
                        body { margin: 5mm; }
                        .no-print { display: none !important; }
                    }
                    " + Nskg.Helpers.ReportPaginationHelper.GetPaginationStyles() + @"
                </style></head><body>");

                sb.Append(Nskg.Helpers.ReportPaginationHelper.GetPaginationToolbarHtml("Station Ledger"));
                sb.Append("<div class='header-box'>");
                sb.Append($"<div class='comp-name'>{compName}</div>");
                sb.Append("<div class='report-title'>G E N E R A L &nbsp;&nbsp; L E D G E R</div>");
                sb.Append("<div class='meta-row'>");
                sb.Append($"<span>Printed On : {DateTime.Now:dd MMMM yyyy}</span>");
                sb.Append($"<span>From : {fromDate:dd MMMM yyyy} &nbsp;&nbsp;&nbsp;&nbsp; To: {toDate:dd MMMM yyyy}</span>");
                sb.Append("<span>Page No. &nbsp;1 &nbsp;of: 1</span>");
                sb.Append("</div>");
                sb.Append("</div>");

                sb.Append("<div class='acc-prominent'>");
                sb.Append($"<span class='acc-code'>{formattedAccode}</span>");
                sb.Append($"<span class='acc-name'>{accName}</span>");
                sb.Append("</div>");

                sb.Append("<table><thead><tr>");
                sb.Append("<th style='width:65px;'>Doc.Date</th>");
                sb.Append("<th style='width:55px;'>Doc. #</th>");
                sb.Append("<th style='width:35px;'>Typ</th>");
                sb.Append("<th style='width:45px;'>Comm</th>");
                sb.Append("<th style='width:80px;'>Vehicle No</th>");
                sb.Append("<th style='width:120px;'>Station</th>");
                sb.Append("<th>Narration</th>");
                sb.Append("<th style='width:55px;'>Comapny</th>");
                sb.Append("<th class='num' style='width:75px;'>Delivery AMT</th>");
                sb.Append("<th class='num' style='width:70px;'>6% Delivery</th>");
                sb.Append("<th class='num' style='width:75px;'>DEBIT</th>");
                sb.Append("<th class='num' style='width:75px;'>CREDIT</th>");
                sb.Append("<th class='num' style='width:85px;'>BALANCE</th>");
                sb.Append("</tr></thead><tbody>");

                foreach (DataRow row in dt.Rows)
                {
                    int sortOrder = row["SortOrder"] != DBNull.Value ? Convert.ToInt32(row["SortOrder"]) : 1;
                    bool isOpening = sortOrder == 0;

                    decimal debit = row["Debit"] != DBNull.Value ? Convert.ToDecimal(row["Debit"]) : 0;
                    decimal credit = row["Credit"] != DBNull.Value ? Convert.ToDecimal(row["Credit"]) : 0;
                    decimal balance = row["Balance"] != DBNull.Value ? Convert.ToDecimal(row["Balance"]) : 0;
                    decimal delAmt = row["DeliveryAmt"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt"]) : 0;
                    decimal delAmt2 = row["DeliveryAmt2"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt2"]) : 0;

                    if (!isOpening)
                    {
                        totalDebit += debit;
                        totalCredit += credit;
                    }

                    string docDate = row["DocDate"] != DBNull.Value && row["DocDate"] != null ? Convert.ToDateTime(row["DocDate"]).ToString("dd-MM-yy") : "";
                    string rowClass = isOpening ? "class='op-row'" : "";

                    sb.Append($"<tr {rowClass}>");
                    sb.Append($"<td>{docDate}</td>");
                    sb.Append($"<td>{row["DocNo"]}</td>");
                    sb.Append($"<td>{row["Votype"]}</td>");
                    sb.Append($"<td>{row["Comm"]}</td>");
                    sb.Append($"<td class='bold'>{row["VehicleNo"]}</td>");
                    sb.Append($"<td>{row["Station"]}</td>");
                    sb.Append($"<td>{row["Narration"]}</td>");
                    sb.Append($"<td>{row["CompanyCode"]}</td>");
                    sb.Append($"<td class='num'>{(delAmt != 0 ? delAmt.ToString("#,##0") : "")}</td>");
                    sb.Append($"<td class='num'>{(delAmt2 != 0 ? delAmt2.ToString("#,##0") : "")}</td>");
                    sb.Append($"<td class='num'>{(debit != 0 ? debit.ToString("#,##0") : "")}</td>");
                    sb.Append($"<td class='num'>{(credit != 0 ? credit.ToString("#,##0") : "")}</td>");
                    sb.Append($"<td class='num bold'>{balance:#,##0}</td>");
                    sb.Append("</tr>");
                }

                decimal closingBal = (dt.Rows.Count > 0 && dt.Rows[dt.Rows.Count - 1]["Balance"] != DBNull.Value) ? Convert.ToDecimal(dt.Rows[dt.Rows.Count - 1]["Balance"]) : 0;

                sb.Append("</tbody><tfoot><tr>");
                sb.Append("<td colspan='10' class='bold' style='text-align:left;'>GRAND TOTAL.........................................................................................................</td>");
                sb.Append($"<td class='num bold'>{totalDebit:#,##0.00}</td>");
                sb.Append($"<td class='num bold'>{totalCredit:#,##0.00}</td>");
                sb.Append($"<td class='num bold'>{closingBal:#,##0.00}</td>");
                sb.Append("</tr></tfoot></table>");
                sb.Append(Nskg.Helpers.ReportPaginationHelper.GetPaginationScript());
                sb.Append("</body></html>");

                return Content(sb.ToString(), "text/html");
            }
            catch (Exception ex)
            {
                return Content($"<div style='color:red; padding:20px;'><strong>Error:</strong> {ex.Message}<br/><pre>{ex.StackTrace}</pre></div>", "text/html");
            }
        }

        [HttpGet]
        public IActionResult ExportExcel(string? accode, DateTime fromDate, DateTime toDate, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetStationLedger(accode ?? "", fromDate, toDate, selectedCompanyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                var sb = new StringBuilder();

                // Company Header
                string compName = (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company";
                string accName = (dt.Rows.Count > 0 ? dt.Rows[0]["AccName"]?.ToString() : null) ?? "";
                sb.AppendLine($"\"{compName}\"");
                sb.AppendLine("\"GENERAL LEDGER (STATION)\"");
                sb.AppendLine($"\"Account:\",\"{accode} - {accName}\",\"From:\",\"{fromDate:dd-MM-yyyy}\",\"To:\",\"{toDate:dd-MM-yyyy}\"");
                sb.AppendLine();

                // Table Header
                sb.AppendLine("Doc.Date,Doc. #,Typ,Comm,Vehicle No,Station,Narration,Comapny,Delivery AMT,6% Delivery,DEBIT,CREDIT,BALANCE");

                foreach (DataRow row in dt.Rows)
                {
                    string docDate = row["DocDate"] != DBNull.Value && row["DocDate"] != null ? Convert.ToDateTime(row["DocDate"]).ToString("dd-MM-yy") : "";
                    string docNo = EscapeCsv(row["DocNo"]?.ToString() ?? "");
                    string votype = EscapeCsv(row["Votype"]?.ToString() ?? "");
                    string comm = EscapeCsv(row["Comm"]?.ToString() ?? "");
                    string vehicleNo = EscapeCsv(row["VehicleNo"]?.ToString() ?? "");
                    string station = EscapeCsv(row["Station"]?.ToString() ?? "");
                    string narration = EscapeCsv(row["Narration"]?.ToString() ?? "");
                    string companyCode = EscapeCsv(row["CompanyCode"]?.ToString() ?? "");
                    string deliveryAmt = row["DeliveryAmt"] != DBNull.Value && row["DeliveryAmt"] != null && Convert.ToDecimal(row["DeliveryAmt"]) != 0 ? Convert.ToDecimal(row["DeliveryAmt"]).ToString("#,##0") : "";
                    string deliveryAmt2 = row["DeliveryAmt2"] != DBNull.Value && row["DeliveryAmt2"] != null && Convert.ToDecimal(row["DeliveryAmt2"]) != 0 ? Convert.ToDecimal(row["DeliveryAmt2"]).ToString("#,##0") : "";
                    string debit = row["Debit"] != DBNull.Value && Convert.ToDecimal(row["Debit"]) != 0 ? Convert.ToDecimal(row["Debit"]).ToString("#,##0") : "";
                    string credit = row["Credit"] != DBNull.Value && Convert.ToDecimal(row["Credit"]) != 0 ? Convert.ToDecimal(row["Credit"]).ToString("#,##0") : "";
                    string balance = row["Balance"] != DBNull.Value ? Convert.ToDecimal(row["Balance"]).ToString("#,##0") : "";

                    sb.AppendLine($"{docDate},{docNo},{votype},{comm},{vehicleNo},{station},{narration},{companyCode},{deliveryAmt},{deliveryAmt2},{debit},{credit},{balance}");
                }

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"StationLedger_{accode}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}");
            }
        }

        private string EscapeCsv(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Contains(",") || text.Contains("\"") || text.Contains("\n") || text.Contains("\r"))
            {
                return $"\"{text.Replace("\"", "\"\"")}\"";
            }
            return text;
        }
    }
}
