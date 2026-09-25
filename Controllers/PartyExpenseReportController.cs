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
    public class PartyExpenseReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public PartyExpenseReportController(ApplicationDbContext context, IConfiguration config, IWebHostEnvironment env)
        {
            _context = context;
            _config = config;
            _env = env;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private int GetCompanyId(int? companyId = null)
        {
            if (companyId.HasValue && companyId.Value > 0) return companyId.Value;
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
            var accounts = _context.GLChart3
                .Where(x => (x.CompanyId == companyId || x.CompanyId == 0 || x.CompanyId == null) && !string.IsNullOrEmpty(x.Name))
                .Select(x => new
                {
                    Code = x.ACC ?? (x.AC1 + x.AC3),
                    Name = x.Name,
                    IsParty = (x.AC1 == "040" || x.AC1 == "075" || x.AC1 == "030" || x.Name.Contains("EXP") || x.Name.Contains("PARTY"))
                })
                .OrderByDescending(x => x.IsParty)
                .ThenBy(x => x.Code)
                .ToList();

            return Json(accounts);
        }

        [HttpGet]
        public IActionResult GetVehiclesByCompany(int companyId)
        {
            var vehicles = _context.VoDet
                .Where(v => v.Vehicleno != null && v.Vehicleno.Trim() != "" && (v.VoHead == null || v.VoHead.CompanyId == companyId || v.VoHead.CompanyId == 0))
                .Select(v => v.Vehicleno!.Trim())
                .Union(_context.ChallanHead
                    .Where(c => c.VehicleNo != null && c.VehicleNo.Trim() != "" && (c.CompanyId == companyId || c.CompanyId == 0))
                    .Select(c => c.VehicleNo!.Trim()))
                .Union(_context.CommHead
                    .Where(c => c.VehicleNo != null && c.VehicleNo.Trim() != "" && (c.CompanyId == companyId || c.CompanyId == 0))
                    .Select(c => c.VehicleNo!.Trim()))
                .Union(_context.ChallanDet
                    .Where(c => c.VehicleNo != null && c.VehicleNo.Trim() != "" && (c.CompanyId == companyId || c.CompanyId == 0))
                    .Select(c => c.VehicleNo!.Trim()))
                .Where(v => v != "")
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            return Json(vehicles);
        }

        [HttpGet]
        public IActionResult Index(string? accode, string? fromDate, string? toDate, string? vehicleNo, string? biltyNo, int? companyId)
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

            // Load Party accounts / Party Expense accounts and all active GL accounts
            var accounts = _context.GLChart3
                .Where(x => (x.CompanyId == selectedCompanyId || x.CompanyId == 0 || x.CompanyId == null) && !string.IsNullOrEmpty(x.Name))
                .Select(x => new
                {
                    Code = x.ACC ?? (x.AC1 + x.AC3),
                    Name = x.Name,
                    IsParty = (x.AC1 == "040" || x.AC1 == "075" || x.AC1 == "030" || x.Name.Contains("EXP") || x.Name.Contains("PARTY"))
                })
                .OrderByDescending(x => x.IsParty)
                .ThenBy(x => x.Code)
                .ToList();

            var vehicles = _context.VoDet
                .Where(v => v.Vehicleno != null && v.Vehicleno.Trim() != "" && (v.VoHead == null || v.VoHead.CompanyId == selectedCompanyId || v.VoHead.CompanyId == 0))
                .Select(v => v.Vehicleno!.Trim())
                .Union(_context.ChallanHead
                    .Where(c => c.VehicleNo != null && c.VehicleNo.Trim() != "" && (c.CompanyId == selectedCompanyId || c.CompanyId == 0))
                    .Select(c => c.VehicleNo!.Trim()))
                .Union(_context.CommHead
                    .Where(c => c.VehicleNo != null && c.VehicleNo.Trim() != "" && (c.CompanyId == selectedCompanyId || c.CompanyId == 0))
                    .Select(c => c.VehicleNo!.Trim()))
                .Union(_context.ChallanDet
                    .Where(c => c.VehicleNo != null && c.VehicleNo.Trim() != "" && (c.CompanyId == selectedCompanyId || c.CompanyId == 0))
                    .Select(c => c.VehicleNo!.Trim()))
                .Where(v => v != "")
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            ViewBag.CompanyList = companies;
            ViewBag.SelectedCompanyId = selectedCompanyId;
            ViewBag.AccountList = accounts;
            ViewBag.VehicleList = vehicles;
            ViewBag.Accode = accode;
            ViewBag.FromDate = string.IsNullOrEmpty(fromDate) ? "2010-01-01" : fromDate;
            ViewBag.ToDate = string.IsNullOrEmpty(toDate) ? DateTime.Now.ToString("yyyy-MM-dd") : toDate;
            ViewBag.VehicleNo = vehicleNo;
            ViewBag.BiltyNo = biltyNo;

            return View();
        }

        private DataTable GetPartyExpenseLedger(string accode, DateTime fromDate, DateTime toDate, int companyId, int financialYearId, string vehicleNo, string biltyNo)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                // 1. Run dbo.PROCESSDETAIL_PartyExp to populate ACCUMULATED & ACCOPEN
                try
                {
                    using (SqlCommand cmdProc = new SqlCommand("dbo.PROCESSDETAIL_PartyExp", con))
                    {
                        cmdProc.CommandType = CommandType.StoredProcedure;
                        cmdProc.CommandTimeout = 180;
                        cmdProc.Parameters.AddWithValue("@CompanyId", companyId);
                        cmdProc.Parameters.AddWithValue("@TDATE", toDate.Date);
                        cmdProc.Parameters.AddWithValue("@ACCODE", accode?.Trim() ?? "");
                        cmdProc.Parameters.AddWithValue("@VehicleNo", vehicleNo?.Trim() ?? "");
                        cmdProc.ExecuteNonQuery();
                    }
                }
                catch
                {
                    try
                    {
                        using (SqlCommand cmdProc2 = new SqlCommand("dbo.PROCESSDETAIL_PartyExp", con))
                        {
                            cmdProc2.CommandType = CommandType.StoredProcedure;
                            cmdProc2.CommandTimeout = 180;
                            cmdProc2.Parameters.AddWithValue("@CompanyId", companyId);
                            cmdProc2.Parameters.AddWithValue("@TDATE", toDate.Date);
                            cmdProc2.Parameters.AddWithValue("@ACCODE", accode?.Trim() ?? "");
                            cmdProc2.ExecuteNonQuery();
                        }
                    }
                    catch
                    {
                        try
                        {
                            using (SqlCommand cmdProc3 = new SqlCommand("dbo.PROCESSDETAIL", con))
                            {
                                cmdProc3.CommandType = CommandType.StoredProcedure;
                                cmdProc3.CommandTimeout = 180;
                                cmdProc3.Parameters.AddWithValue("@CompanyId", companyId);
                                cmdProc3.Parameters.AddWithValue("@TDATE", toDate.Date);
                                cmdProc3.Parameters.AddWithValue("@ACCODE", accode?.Trim() ?? "");
                                cmdProc3.ExecuteNonQuery();
                            }
                        }
                        catch { }
                    }
                }

                // 2. Fetch Account Name & Company Name
                string accName = "ALL PARTY / EXPENSE ACCOUNTS";
                if (!string.IsNullOrWhiteSpace(accode))
                {
                    using (SqlCommand cmdAcc = new SqlCommand(
                        "SELECT TOP 1 Name FROM GLCHART3 WHERE (CompanyId = @CompanyId OR CompanyId = 0 OR CompanyId IS NULL) AND (RTRIM(AC1) + RTRIM(AC3) = RTRIM(@Accode) OR ACC = RTRIM(@Accode))", con))
                    {
                        cmdAcc.Parameters.AddWithValue("@CompanyId", companyId);
                        cmdAcc.Parameters.AddWithValue("@Accode", accode.Trim());
                        var res = cmdAcc.ExecuteScalar();
                        if (res != null && res != DBNull.Value) accName = res.ToString();
                    }
                }
                if (!string.IsNullOrWhiteSpace(vehicleNo))
                {
                    if (string.IsNullOrWhiteSpace(accode))
                        accName = $"VEHICLE EXPENSE ({vehicleNo.Trim()})";
                    else
                        accName += $" (VEHICLE: {vehicleNo.Trim()})";
                }

                string companyName = "West Wharf-New Shadab Karachi Goods Transports";
                using (SqlCommand cmdComp = new SqlCommand("SELECT TOP 1 Name FROM Companies WHERE Id = @CompanyId", con))
                {
                    cmdComp.Parameters.AddWithValue("@CompanyId", companyId);
                    var res = cmdComp.ExecuteScalar();
                    if (res != null && res != DBNull.Value && !string.IsNullOrWhiteSpace(res.ToString()))
                        companyName = res.ToString();
                }

                // 3. Query Opening Balance and Transactions with Running Balance from ACCUMULATED
                string query = @"
                    DECLARE @AnnualOpeningBal DECIMAL(18,2) = 0;

                    IF OBJECT_ID('OpeningBalances', 'U') IS NOT NULL
                    BEGIN
                        SELECT @AnnualOpeningBal = ISNULL(SUM(Debit - Credit), 0)
                        FROM OpeningBalances
                        WHERE (@Accode = '' OR Accode = @Accode)
                          AND CompanyId = @CompanyId
                          AND FinancialYearId = @FinancialYearId;
                    END

                    DECLARE @OpeningBal DECIMAL(18,2) = 0;

                    SELECT @OpeningBal = @AnnualOpeningBal + ISNULL(SUM(ISNULL(DRAMT, 0) - ISNULL(CRAMT, 0)), 0)
                    FROM ACCUMULATED a
                    WHERE a.VODATE < @FromDate
                      AND (@Accode = '' OR RTRIM(LTRIM(ISNULL(a.ACC, ''))) = @Accode OR (RTRIM(LTRIM(ISNULL(a.AC1, ''))) + RTRIM(LTRIM(ISNULL(a.AC3, '')))) = @Accode)
                      AND (
                          @VehicleNo = '' 
                          OR RTRIM(LTRIM(ISNULL(a.VEHICLENO, ''))) = RTRIM(LTRIM(@VehicleNo)) 
                          OR (a.VOTYPE = 'CL' AND RTRIM(LTRIM(ISNULL(a.NARRATION, ''))) = RTRIM(LTRIM(@VehicleNo)))
                          OR a.VEHICLENO LIKE '%' + @VehicleNo + '%'
                          OR (a.VOTYPE = 'CL' AND a.NARRATION LIKE '%' + @VehicleNo + '%')
                          OR EXISTS (SELECT 1 FROM ISSHEAD ish WHERE (ish.BillTiNo = a.BILLTINO OR ish.BilNo = a.BILNO) AND ish.VehicleNo LIKE '%' + @VehicleNo + '%')
                          OR EXISTS (SELECT 1 FROM ChallanDet cd INNER JOIN ChallanHead ch ON cd.ChallanHeadId = ch.Id WHERE (cd.BillTiNo = a.BILLTINO OR cd.BilNo = a.BILNO) AND ch.VehicleNo LIKE '%' + @VehicleNo + '%')
                          OR EXISTS (SELECT 1 FROM CommDetail cmd INNER JOIN CommHead cm ON cmd.CommHeadId = cm.Id WHERE cmd.BillTiNo = a.BILLTINO AND cm.VehicleNo LIKE '%' + @VehicleNo + '%')
                      )
                      AND (@BiltyNo = '' OR CAST(a.BILNO AS VARCHAR) = @BiltyNo OR CAST(a.BILLTINO AS VARCHAR) = @BiltyNo);

                    ;WITH RawData AS
                    (
                        -- Opening Balance Row
                        SELECT 
                            0 AS SortOrder,
                            CAST(NULL AS DATE) AS DocDate,
                            CAST('' AS VARCHAR(50)) AS DocNo,
                            CAST('' AS VARCHAR(10)) AS Votype,
                            CAST(NULL AS VARCHAR(50)) AS BillTiNo,
                            CAST(NULL AS VARCHAR(50)) AS BilNo,
                            CAST('OPENING BALANCE' AS VARCHAR(100)) AS VehicleNo,
                            CAST('' AS VARCHAR(100)) AS Station,
                            CAST('' AS VARCHAR(250)) AS IName,
                            CAST(NULL AS DECIMAL(18,2)) AS Qty,
                            CASE WHEN @OpeningBal > 0 THEN @OpeningBal ELSE CAST(0 AS DECIMAL(18,2)) END AS Debit,
                            CASE WHEN @OpeningBal < 0 THEN ABS(@OpeningBal) ELSE CAST(0 AS DECIMAL(18,2)) END AS Credit,
                            @OpeningBal AS Balance,
                            CAST(0 AS BIGINT) AS RowNum

                        UNION ALL

                        -- Transactions between FromDate and ToDate
                        SELECT 
                            1 AS SortOrder,
                            CAST(a.VODATE AS DATE) AS DocDate,
                            ISNULL(a.VONO, '') AS DocNo,
                            ISNULL(a.VOTYPE, '') AS Votype,
                            CASE WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN CAST(CAST(a.BILLTINO AS BIGINT) AS VARCHAR(50)) ELSE NULL END AS BillTiNo,
                            CASE WHEN a.BILNO IS NOT NULL AND a.BILNO <> 0 THEN CAST(CAST(a.BILNO AS BIGINT) AS VARCHAR(50)) ELSE NULL END AS BilNo,
                            CASE 
                                WHEN NULLIF(RTRIM(LTRIM(a.VEHICLENO)), '') IS NOT NULL THEN RTRIM(LTRIM(a.VEHICLENO))
                                ELSE COALESCE(
                                    (SELECT TOP 1 ish.VehicleNo FROM ISSHEAD ish WHERE (ish.BillTiNo = a.BILLTINO OR ish.BilNo = a.BILNO) AND NULLIF(RTRIM(LTRIM(ish.VehicleNo)), '') IS NOT NULL),
                                    (SELECT TOP 1 ch.VehicleNo FROM ChallanDet cd INNER JOIN ChallanHead ch ON cd.ChallanHeadId = ch.Id WHERE (cd.BillTiNo = a.BILLTINO OR cd.BilNo = a.BILNO) AND NULLIF(RTRIM(LTRIM(ch.VehicleNo)), '') IS NOT NULL),
                                    (SELECT TOP 1 cm.VehicleNo FROM CommDetail cmd INNER JOIN CommHead cm ON cmd.CommHeadId = cm.Id WHERE cmd.BillTiNo = a.BILLTINO AND NULLIF(RTRIM(LTRIM(cm.VehicleNo)), '') IS NOT NULL),
                                    CASE WHEN a.VOTYPE = 'CL' THEN NULLIF(RTRIM(LTRIM(a.NARRATION)), '') ELSE NULL END,
                                    ''
                                )
                            END AS VehicleNo,
                            ISNULL(a.STATION, '') AS Station,
                            CASE 
                                WHEN a.VOTYPE = 'CL' THEN ISNULL(a.INAME, '')
                                ELSE ISNULL(a.INAME, ISNULL(a.NARRATION, ''))
                            END AS IName,
                            a.QTY AS Qty,
                            ISNULL(a.DRAMT, 0) AS Debit,
                            ISNULL(a.CRAMT, 0) AS Credit,
                            @OpeningBal + SUM(ISNULL(a.DRAMT, 0) - ISNULL(a.CRAMT, 0)) OVER (
                                ORDER BY a.VODATE, a.VONO, (SELECT NULL)
                                ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                            ) AS Balance,
                            ROW_NUMBER() OVER (ORDER BY a.VODATE, a.VONO) AS RowNum
                        FROM ACCUMULATED a
                        WHERE a.VODATE >= @FromDate AND a.VODATE <= @ToDate
                          AND (@Accode = '' OR RTRIM(LTRIM(ISNULL(a.ACC, ''))) = @Accode OR (RTRIM(LTRIM(ISNULL(a.AC1, ''))) + RTRIM(LTRIM(ISNULL(a.AC3, '')))) = @Accode)
                          AND (
                              @VehicleNo = '' 
                              OR RTRIM(LTRIM(ISNULL(a.VEHICLENO, ''))) = RTRIM(LTRIM(@VehicleNo)) 
                              OR (a.VOTYPE = 'CL' AND RTRIM(LTRIM(ISNULL(a.NARRATION, ''))) = RTRIM(LTRIM(@VehicleNo)))
                              OR a.VEHICLENO LIKE '%' + @VehicleNo + '%'
                              OR (a.VOTYPE = 'CL' AND a.NARRATION LIKE '%' + @VehicleNo + '%')
                              OR EXISTS (SELECT 1 FROM ISSHEAD ish WHERE (ish.BillTiNo = a.BILLTINO OR ish.BilNo = a.BILNO) AND ish.VehicleNo LIKE '%' + @VehicleNo + '%')
                              OR EXISTS (SELECT 1 FROM ChallanDet cd INNER JOIN ChallanHead ch ON cd.ChallanHeadId = ch.Id WHERE (cd.BillTiNo = a.BILLTINO OR cd.BilNo = a.BILNO) AND ch.VehicleNo LIKE '%' + @VehicleNo + '%')
                              OR EXISTS (SELECT 1 FROM CommDetail cmd INNER JOIN CommHead cm ON cmd.CommHeadId = cm.Id WHERE cmd.BillTiNo = a.BILLTINO AND cm.VehicleNo LIKE '%' + @VehicleNo + '%')
                          )
                          AND (@BiltyNo = '' OR CAST(a.BILNO AS VARCHAR) = @BiltyNo OR CAST(a.BILLTINO AS VARCHAR) = @BiltyNo)
                    )
                    SELECT 
                        SortOrder,
                        DocDate,
                        DocNo,
                        Votype,
                        BillTiNo,
                        BilNo,
                        VehicleNo,
                        Station,
                        INAME,
                        Qty,
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
                    cmdData.Parameters.AddWithValue("@Accode", string.IsNullOrWhiteSpace(accode) ? "" : accode.Trim());
                    cmdData.Parameters.AddWithValue("@AccName", accName);
                    cmdData.Parameters.AddWithValue("@CompanyName", companyName);
                    cmdData.Parameters.AddWithValue("@CompanyId", companyId);
                    cmdData.Parameters.AddWithValue("@FinancialYearId", financialYearId);
                    cmdData.Parameters.AddWithValue("@VehicleNo", string.IsNullOrWhiteSpace(vehicleNo) ? "" : vehicleNo.Trim());
                    cmdData.Parameters.AddWithValue("@BiltyNo", string.IsNullOrWhiteSpace(biltyNo) ? "" : biltyNo.Trim());

                    using (SqlDataReader reader = cmdData.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult GeneratePDF(string? accode, DateTime? fromDate, DateTime? toDate, string? vehicleNo, string? biltyNo, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();
            DateTime effFromDate = fromDate ?? DateTime.Now.AddDays(-30);
            DateTime effToDate = toDate ?? DateTime.Now;

            try
            {
                DataTable dt = GetPartyExpenseLedger(accode ?? "", effFromDate, effToDate, selectedCompanyId, financialYearId, vehicleNo ?? "", biltyNo ?? "");

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found for selected date range and filter criteria.");
                }

                string reportPath = Path.Combine(_env.WebRootPath, "Reports", "AccountLedgerrpt.rdlc");

                if (!System.IO.File.Exists(reportPath))
                {
                    return Content($"Report file not found: {reportPath}");
                }

                using (LocalReport report = new LocalReport())
                {
                    report.ReportPath = reportPath;
                    report.DataSources.Clear();

                    dt.TableName = "DSAccountLedger";
                    report.DataSources.Add(new ReportDataSource("DSAccountLedger", dt));

                    byte[] pdfBytes = report.Render("PDF");

                    if (pdfBytes == null || pdfBytes.Length < 100)
                    {
                        return Content("PDF generation failed or corrupted output.");
                    }

                    string label = !string.IsNullOrWhiteSpace(accode) ? accode : (!string.IsNullOrWhiteSpace(vehicleNo) ? $"Vehicle_{vehicleNo.Trim()}" : "ExpenseLedger");
                    return File(pdfBytes, "application/pdf", $"PartyExpenseLedger_{label}_{effFromDate:yyyyMMdd}_{effToDate:yyyyMMdd}.pdf", enableRangeProcessing: true);
                }
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult OnScreenReport(string? accode, DateTime? fromDate, DateTime? toDate, string? vehicleNo, string? biltyNo, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();
            DateTime effFromDate = fromDate ?? DateTime.Now.AddDays(-30);
            DateTime effToDate = toDate ?? DateTime.Now;

            try
            {
                DataTable dt = GetPartyExpenseLedger(accode ?? "", effFromDate, effToDate, selectedCompanyId, financialYearId, vehicleNo ?? "", biltyNo ?? "");

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No data found for the selected filter criteria.</strong></div>", "text/html");
                }

                string compName = (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company";
                string accName = (dt.Rows.Count > 0 ? dt.Rows[0]["AccName"]?.ToString() : null) ?? "";

                decimal totalDebit = 0, totalCredit = 0;
                var sb = new StringBuilder();
                sb.Append(@"<!DOCTYPE html><html><head><meta charset='utf-8'>
                <style>
                    body{font-family:Arial,sans-serif;font-size:12px;margin:12px;color:#333;}
                    .header-box{text-align:center;margin-bottom:12px;}
                    .header-box h2{margin:0 0 4px;color:#0d6efd;font-size:18px;}
                    .header-box h3{margin:0 0 4px;font-size:14px;color:#495057;}
                    .header-box p{margin:0;font-size:11px;color:#6c757d;}
                    table{width:100%;border-collapse:collapse;margin-top:8px;}
                    th{background:#0d6efd;color:#fff;padding:7px 6px;text-align:left;font-size:11px;border:1px solid #0b5ed7;}
                    td{padding:5px 6px;border:1px solid #dee2e6;font-size:11px;}
                    tr:nth-child(even){background:#f8f9fa;}
                    tr:hover{background:#e9ecef;}
                    .num{text-align:right;}
                    .center{text-align:center;}
                    .bold{font-weight:bold;}
                    .op-row{background:#e7f1ff;font-weight:bold;}
                    tfoot tr{background:#d0e2ff;font-weight:bold;}
                </style></head><body>");

                sb.Append($"<div class='header-box'>");
                sb.Append($"<h2>{compName}</h2>");
                sb.Append($"<h3>PARTY / EXPENSE LEDGER</h3>");
                string extraFilters = "";
                if (!string.IsNullOrWhiteSpace(vehicleNo)) extraFilters += $" &nbsp;|&nbsp; Vehicle: <b>{vehicleNo}</b>";
                if (!string.IsNullOrWhiteSpace(biltyNo)) extraFilters += $" &nbsp;|&nbsp; Bilty: <b>{biltyNo}</b>";
                sb.Append($"<p>Account: <b>{(string.IsNullOrEmpty(accode) ? "ALL ACCOUNTS" : accode + " - " + accName)}</b> &nbsp;|&nbsp; Period: <b>{effFromDate:dd-MMM-yyyy}</b> to <b>{effToDate:dd-MMM-yyyy}</b>{extraFilters}</p>");
                sb.Append("</div>");

                sb.Append("<table><thead><tr>");
                sb.Append("<th style='width:35px;' class='center'>#</th>");
                sb.Append("<th style='width:75px;'>Date</th>");
                sb.Append("<th style='width:65px;'>Doc No</th>");
                sb.Append("<th style='width:45px;'>Type</th>");
                sb.Append("<th style='width:65px;'>Bilty No</th>");
                sb.Append("<th style='width:60px;'>Bil No</th>");
                sb.Append("<th style='width:85px;'>Vehicle No</th>");
                sb.Append("<th>Station</th>");
                sb.Append("<th>Item / Narration</th>");
                sb.Append("<th class='num' style='width:45px;'>Qty</th>");
                sb.Append("<th class='num' style='width:80px;'>Debit</th>");
                sb.Append("<th class='num' style='width:80px;'>Credit</th>");
                sb.Append("<th class='num' style='width:85px;'>Balance</th>");
                sb.Append("</tr></thead><tbody>");

                int sr = 1;
                foreach (DataRow row in dt.Rows)
                {
                    int sortOrder = row["SortOrder"] != DBNull.Value ? Convert.ToInt32(row["SortOrder"]) : 1;
                    bool isOpening = sortOrder == 0;

                    decimal debit = row["Debit"] != DBNull.Value ? Convert.ToDecimal(row["Debit"]) : 0;
                    decimal credit = row["Credit"] != DBNull.Value ? Convert.ToDecimal(row["Credit"]) : 0;
                    decimal balance = row["Balance"] != DBNull.Value ? Convert.ToDecimal(row["Balance"]) : 0;

                    if (!isOpening)
                    {
                        totalDebit += debit;
                        totalCredit += credit;
                    }

                    string docDate = row["DocDate"] != DBNull.Value ? Convert.ToDateTime(row["DocDate"]).ToString("dd-MMM-yy") : "";
                    string rowClass = isOpening ? "class='op-row'" : "";

                    sb.Append($"<tr {rowClass}>");
                    sb.Append($"<td class='center'>{(isOpening ? "" : sr++.ToString())}</td>");
                    sb.Append($"<td>{docDate}</td>");
                    sb.Append($"<td>{row["DocNo"]}</td>");
                    sb.Append($"<td>{row["Votype"]}</td>");
                    sb.Append($"<td>{row["BillTiNo"]}</td>");
                    sb.Append($"<td>{row["BilNo"]}</td>");
                    sb.Append($"<td class='bold'>{row["VehicleNo"]}</td>");
                    sb.Append($"<td>{row["Station"]}</td>");
                    sb.Append($"<td>{row["INAME"]}</td>");
                    sb.Append($"<td class='num'>{row["Qty"]}</td>");
                    sb.Append($"<td class='num'>{(debit != 0 ? debit.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='num'>{(credit != 0 ? credit.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='num bold'>{balance:#,##0.00}</td>");
                    sb.Append("</tr>");
                }

                decimal closingBal = (dt.Rows.Count > 0 && dt.Rows[dt.Rows.Count - 1]["Balance"] != DBNull.Value) ? Convert.ToDecimal(dt.Rows[dt.Rows.Count - 1]["Balance"]) : 0;

                sb.Append("</tbody><tfoot><tr>");
                sb.Append($"<td colspan='10' class='bold' style='text-align:right;'>TOTAL:</td>");
                sb.Append($"<td class='num bold'>{totalDebit:#,##0.00}</td>");
                sb.Append($"<td class='num bold'>{totalCredit:#,##0.00}</td>");
                sb.Append($"<td class='num bold'>{closingBal:#,##0.00}</td>");
                sb.Append("</tr></tfoot></table></body></html>");

                return Content(sb.ToString(), "text/html");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult ExportExcel(string? accode, DateTime? fromDate, DateTime? toDate, string? vehicleNo, string? biltyNo, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();
            DateTime effFromDate = fromDate ?? DateTime.Now.AddDays(-30);
            DateTime effToDate = toDate ?? DateTime.Now;

            try
            {
                DataTable dt = GetPartyExpenseLedger(accode ?? "", effFromDate, effToDate, selectedCompanyId, financialYearId, vehicleNo ?? "", biltyNo ?? "");

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                var sb = new StringBuilder();
                sb.AppendLine("\"PARTY EXPENSE LEDGER REPORT\"");
                sb.AppendLine($"\"Account:\",\"{accode}\",\"From:\",\"{effFromDate:dd-MM-yyyy}\",\"To:\",\"{effToDate:dd-MM-yyyy}\"");
                sb.AppendLine();
                sb.AppendLine("Doc.Date,Doc. #,Typ,BiltiNo,BIL No,Vehicle No,Station,Party name / Item,Qty,DEBIT,CREDIT,BALANCE");

                foreach (DataRow row in dt.Rows)
                {
                    string docDate = row["DocDate"] != DBNull.Value && row["DocDate"] != null ? Convert.ToDateTime(row["DocDate"]).ToString("dd-MM-yyyy") : "";
                    string docNo = EscapeCsv(row["DocNo"]?.ToString() ?? "");
                    string votype = EscapeCsv(row["Votype"]?.ToString() ?? "");
                    string biltiNo = EscapeCsv(row["BillTiNo"]?.ToString() ?? "");
                    string bilNo = EscapeCsv(row["BilNo"]?.ToString() ?? "");
                    string vNo = EscapeCsv(row["VehicleNo"]?.ToString() ?? "");
                    string station = EscapeCsv(row["Station"]?.ToString() ?? "");
                    string iname = EscapeCsv(row["INAME"]?.ToString() ?? "");
                    string qty = row["Qty"] != DBNull.Value && row["Qty"] != null ? Convert.ToDecimal(row["Qty"]).ToString("#,##0") : "";
                    string debit = row["Debit"] != DBNull.Value && row["Debit"] != null ? Convert.ToDecimal(row["Debit"]).ToString("#,##0.00") : "0.00";
                    string credit = row["Credit"] != DBNull.Value && row["Credit"] != null ? Convert.ToDecimal(row["Credit"]).ToString("#,##0.00") : "0.00";
                    string balance = row["Balance"] != DBNull.Value && row["Balance"] != null ? Convert.ToDecimal(row["Balance"]).ToString("#,##0.00") : "0.00";

                    sb.AppendLine($"{docDate},{docNo},{votype},{biltiNo},{bilNo},{vNo},{station},{iname},{qty},{debit},{credit},{balance}");
                }

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"PartyExpenseLedger_{accode}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
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
                return $"\"{text.Replace("\"", "\"\"")}\"";
            return text;
        }
    }
}
