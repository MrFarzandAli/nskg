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
                .Where(x => x.CompanyId == companyId || x.CompanyId == 0 || x.CompanyId == null)
                .Select(x => new
                {
                    Code = x.ACC ?? (x.AC1 + x.AC3),
                    Name = x.Name
                })
                .OrderBy(x => x.Code)
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

            var accounts = _context.GLChart3
                .Where(x => x.CompanyId == selectedCompanyId || x.CompanyId == 0 || x.CompanyId == null)
                .Select(x => new
                {
                    Code = x.ACC ?? (x.AC1 + x.AC3),
                    Name = x.Name
                })
                .OrderBy(x => x.Code)
                .ToList();

            ViewBag.CompanyList = companies;
            ViewBag.SelectedCompanyId = selectedCompanyId;
            ViewBag.AccountList = accounts;
            ViewBag.Accode = accode;
            ViewBag.FromDate = string.IsNullOrEmpty(fromDate) ? DateTime.Now.ToString("yyyy-MM-dd") : fromDate;
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
                using (SqlCommand cmdProc = new SqlCommand("dbo.PROCESSDETAIL_Station", con))
                {
                    cmdProc.CommandType = CommandType.StoredProcedure;
                    cmdProc.CommandTimeout = 180;
                    cmdProc.Parameters.AddWithValue("@CompanyId", companyId);
                    cmdProc.Parameters.AddWithValue("@TDATE", toDate.Date);
                    cmdProc.Parameters.AddWithValue("@ACCODE", accode?.Trim() ?? "");
                    cmdProc.ExecuteNonQuery();
                }

                // 2. Fetch Account Name & Company Name
                string accName = "";
                using (SqlCommand cmdAcc = new SqlCommand(
                    "SELECT TOP 1 Name FROM GLCHART3 WHERE (CompanyId = @CompanyId OR CompanyId = 0 OR CompanyId IS NULL) AND (RTRIM(AC1) + RTRIM(AC3) = RTRIM(@Accode) OR ACC = RTRIM(@Accode))", con))
                {
                    cmdAcc.Parameters.AddWithValue("@CompanyId", companyId);
                    cmdAcc.Parameters.AddWithValue("@Accode", accode?.Trim() ?? "");
                    var res = cmdAcc.ExecuteScalar();
                    if (res != null && res != DBNull.Value) accName = res.ToString();
                }

                string companyName = "West Wharf-New Shadab Karachi Goods Transports";
                using (SqlCommand cmdComp = new SqlCommand("SELECT TOP 1 Name FROM Companies WHERE Id = @CompanyId", con))
                {
                    cmdComp.Parameters.AddWithValue("@CompanyId", companyId);
                    var res = cmdComp.ExecuteScalar();
                    if (res != null && res != DBNull.Value && !string.IsNullOrWhiteSpace(res.ToString()))
                        companyName = res.ToString();
                }

                // 3. Query Opening Balance and Transactions with Running Balance
                string query = @"
                    DECLARE @AnnualOpeningBal DECIMAL(18,2) = 0;

                    IF OBJECT_ID('OpeningBalances', 'U') IS NOT NULL
                    BEGIN
                        SELECT @AnnualOpeningBal = ISNULL(SUM(Debit - Credit), 0)
                        FROM OpeningBalances
                        WHERE Accode = @Accode
                          AND CompanyId = @CompanyId
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
                            CAST('Balance Brought Farword' AS VARCHAR(100)) AS VehicleNo,
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
                            ISNULL(a.VONO, '') AS DocNo,
                            ISNULL(a.VOTYPE, '') AS Votype,
                            CASE WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN CAST(CAST(a.BILLTINO AS BIGINT) AS VARCHAR(50)) ELSE NULL END AS Comm,
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
                            ISNULL(a.INAME, '') AS Station,
                            ISNULL(a.NARRATION, '') AS Narration,
                            'W.H' AS CompanyCode,
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
                sb.Append($"<h3>STATION LEDGER REPORT</h3>");
                sb.Append($"<p>Account: <b>{(string.IsNullOrEmpty(accode) ? "ALL STATIONS" : accode + " - " + accName)}</b> &nbsp;|&nbsp; Period: <b>{fromDate:dd-MMM-yyyy}</b> to <b>{toDate:dd-MMM-yyyy}</b></p>");
                sb.Append("</div>");

                sb.Append("<table><thead><tr>");
                sb.Append("<th style='width:35px;' class='center'>#</th>");
                sb.Append("<th style='width:75px;'>Doc Date</th>");
                sb.Append("<th style='width:65px;'>Doc #</th>");
                sb.Append("<th style='width:45px;'>Type</th>");
                sb.Append("<th style='width:65px;'>Comm</th>");
                sb.Append("<th style='width:85px;'>Vehicle No</th>");
                sb.Append("<th>Station</th>");
                sb.Append("<th>Narration</th>");
                sb.Append("<th style='width:50px;'>Branch</th>");
                sb.Append("<th class='num' style='width:75px;'>Delivery</th>");
                sb.Append("<th class='num' style='width:65px;'>6% Del.</th>");
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
                    decimal delAmt = row["DeliveryAmt"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt"]) : 0;
                    decimal delAmt2 = row["DeliveryAmt2"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt2"]) : 0;

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
                    sb.Append($"<td>{row["Comm"]}</td>");
                    sb.Append($"<td class='bold'>{row["VehicleNo"]}</td>");
                    sb.Append($"<td>{row["Station"]}</td>");
                    sb.Append($"<td>{row["Narration"]}</td>");
                    sb.Append($"<td>{row["CompanyCode"]}</td>");
                    sb.Append($"<td class='num'>{(delAmt != 0 ? delAmt.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='num'>{(delAmt2 != 0 ? delAmt2.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='num'>{(debit != 0 ? debit.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='num'>{(credit != 0 ? credit.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='num bold'>{balance:#,##0.00}</td>");
                    sb.Append("</tr>");
                }

                decimal closingBal = (dt.Rows.Count > 0 && dt.Rows[dt.Rows.Count - 1]["Balance"] != DBNull.Value) ? Convert.ToDecimal(dt.Rows[dt.Rows.Count - 1]["Balance"]) : 0;

                sb.Append("</tbody><tfoot><tr>");
                sb.Append($"<td colspan='11' class='bold' style='text-align:right;'>TOTAL:</td>");
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
                sb.AppendLine("Doc.Date,Doc. #,Typ,Comm,Vehicle No,Station,Narration,Company,Delivery AMT,6% Delivery,DEBIT,CREDIT,BALANCE");

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
