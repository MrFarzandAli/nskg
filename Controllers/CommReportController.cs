using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Reporting.NETCore;
using Nskg.Data;
using Nskg.Extensions;
using System;
using System.Data;
using System.IO;
using System.Text;

namespace Nskg.Controllers
{
    [Authorize]
    public class CommReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public CommReportController(ApplicationDbContext context, IConfiguration config, IWebHostEnvironment env)
        {
            _context = context;
            _config = config;
            _env = env;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private int GetCompanyId()
        {
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
        public IActionResult Index(string fromDate, string toDate, string partyCode, string reportType)
        {
            ViewBag.FromDate = string.IsNullOrEmpty(fromDate) ? DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd") : fromDate;
            ViewBag.ToDate = string.IsNullOrEmpty(toDate) ? DateTime.Now.ToString("yyyy-MM-dd") : toDate;
            ViewBag.PartyCode = partyCode ?? "";
            ViewBag.ReportType = string.IsNullOrEmpty(reportType) ? "Godown" : reportType;

            return View("Index");
        }

        [HttpGet]
        public IActionResult ChallanList(string fromDate, string toDate, string partyCode)
        {
            return Index(fromDate, toDate, partyCode, "Godown");
        }

        [HttpGet]
        public IActionResult ChallanReport(string fromDate, string toDate, string partyCode)
        {
            return Index(fromDate, toDate, partyCode, "Adda");
        }

        /// <summary>
        /// Vehicle List Procedure & Converted Oracle Query for commreport_all1 (Godown) and commreport_all12 (Adda)
        /// </summary>
        private DataTable GetCommReportData(DateTime? fromDate, DateTime? toDate, string partyCode, string reportType, int companyId)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                // Fetch Company Name
                string companyName = "New Shadab Karachi Goods Transports";
                using (SqlCommand cmdComp = new SqlCommand("SELECT TOP 1 Name FROM Companies WHERE Id = @CompanyId", con))
                {
                    cmdComp.Parameters.AddWithValue("@CompanyId", companyId);
                    var res = cmdComp.ExecuteScalar();
                    if (res != null && res != DBNull.Value && !string.IsNullOrWhiteSpace(res.ToString()))
                        companyName = res.ToString();
                }

                // Filter by location type (Godown vs Adda) if station name or type is passed
                string locationFilter = "";
                if (!string.IsNullOrWhiteSpace(reportType))
                {
                    if (reportType.Equals("Godown", StringComparison.OrdinalIgnoreCase))
                    {
                        locationFilter = " AND (h.Station LIKE '%Godown%' OR h.Station NOT LIKE '%Adda%') ";
                    }
                    else if (reportType.Equals("Adda", StringComparison.OrdinalIgnoreCase))
                    {
                        locationFilter = " AND (h.Station LIKE '%Adda%' OR h.Station NOT LIKE '%Godown%') ";
                    }
                }

                string query = $@"
                    SELECT
                        d.DocDate                                       AS VoDate,
                        CAST(ISNULL(d.BillTiNo, 0) AS VARCHAR(20))     AS BillTiNo,
                        ISNULL(h.Station, '')                           AS Station,
                        ISNULL(d.CommPer, 0)                            AS Rate,
                        ISNULL(d.MAmount, 0)                            AS Amount,
                        ISNULL(COALESCE(d.VehicleNo, h.VehicleNo), '')  AS VehicleNo,

                        ISNULL(v.DrAmt, 0)                              AS DrAmt,
                        ISNULL(v.CrAmt, 0)                              AS CrAmt,

                        ISNULL(d.DeliveryAmt, 0)                        AS DeliveryAmt,

                        ISNULL(v.NDrAmt, '')                            AS NDrAmt,
                        ISNULL(v.NCrAmt, '')                            AS NCrAmt,

                        ISNULL(g.AC1, '') + ISNULL(g.AC3, '')           AS PartyCode,

                        ISNULL(d.STaxAmt, 0)                            AS STaxAmt,
                        ISNULL(d.DeliveryAmt2, 0)                       AS DeliveryAmt2,

                        ISNULL(iss.IName, '')                           AS IName,

                        CASE c.Cocode
                            WHEN '01' THEN 'W.H'
                            WHEN '02' THEN 'M.P'
                            WHEN '03' THEN 'N.K'
                            WHEN '04' THEN 'R.W'
                            ELSE ISNULL(c.Cocode, '')
                        END                                             AS Branch,

                        ISNULL(v.Pay, 0)                                AS Pay,
                        ISNULL(d.Qty, 0)                                AS Qty,

                        @CompanyName                                    AS CompanyName,
                        @FromDate                                       AS FromDate,
                        @ToDate                                         AS ToDate,
                        @ReportType                                     AS ReportType

                    FROM CommDetail d
                    INNER JOIN CommHead h ON d.CommHeadId = h.Id
                    LEFT JOIN GLChart3 g  ON g.Id = h.StationId
                    LEFT JOIN ISSHEAD iss ON iss.BillTiNo = d.BillTiNo
                                         AND iss.CompanyId = h.CompanyId
                                         AND ISNULL(iss.IsDeleted, 0) = 0
                    LEFT JOIN Companies c ON c.Id = h.CompanyId

                    LEFT JOIN (
                        SELECT
                            Billtino,
                            Cocode,
                            SUM(ISNULL(Dramt, 0))   AS DrAmt,
                            SUM(ISNULL(Cramt, 0))   AS CrAmt,
                            MAX(Ndramt)              AS NDrAmt,
                            MAX(Ncramt)              AS NCrAmt,
                            SUM(ISNULL(Pay, 0))      AS Pay
                        FROM VoDet
                        WHERE IsDeleted = 0
                        GROUP BY Billtino, Cocode
                    ) v ON v.Billtino = d.BillTiNo AND v.Cocode = c.Cocode

                    WHERE h.CompanyId = @CompanyId
                      AND ISNULL(h.IsDeleted, 0) = 0
                      AND (@FromDate IS NULL OR h.DocDate >= @FromDate)
                      AND (@ToDate   IS NULL OR h.DocDate <= @ToDate)
                      AND (
                            @PartyCode IS NULL
                            OR @PartyCode = ''
                            OR (ISNULL(g.AC1, '') + ISNULL(g.AC3, '')) = @PartyCode
                          )
                      {locationFilter}
                    ORDER BY h.DocDate, d.BillTiNo
                ";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    cmd.Parameters.AddWithValue("@CompanyName", companyName);
                    cmd.Parameters.AddWithValue("@FromDate", fromDate.HasValue ? (object)fromDate.Value.Date : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ToDate", toDate.HasValue ? (object)toDate.Value.Date : DBNull.Value);
                    cmd.Parameters.AddWithValue("@PartyCode", string.IsNullOrWhiteSpace(partyCode) ? (object)DBNull.Value : partyCode.Trim());
                    cmd.Parameters.AddWithValue("@ReportType", string.IsNullOrWhiteSpace(reportType) ? "Godown" : reportType);

                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult GeneratePDF(DateTime? fromDate, DateTime? toDate, string partyCode, string reportType)
        {
            int companyId = GetCompanyId();

            try
            {
                DataTable dt = GetCommReportData(fromDate, toDate, partyCode, reportType, companyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found for the selected filter criteria.");
                }

                // Choose RDLC based on report type: Godown -> CommReportrpt.rdlc, Adda -> CommReportAddarpt.rdlc
                string rdlcFile = (reportType == "Adda") ? "CommReportAddarpt.rdlc" : "CommReportrpt.rdlc";
                string reportPath = Path.Combine(_env.WebRootPath, "Reports", rdlcFile);

                if (!System.IO.File.Exists(reportPath))
                {
                    // Fallback to main rdlc if specific one not found
                    reportPath = Path.Combine(_env.WebRootPath, "Reports", "CommReportrpt.rdlc");
                }

                using (LocalReport report = new LocalReport())
                {
                    report.ReportPath = reportPath;
                    report.DataSources.Clear();

                    dt.TableName = "DSCommReport";
                    report.DataSources.Add(new ReportDataSource("DSCommReport", dt));

                    byte[] pdfBytes = report.Render("PDF");

                    if (pdfBytes == null || pdfBytes.Length < 100)
                    {
                        return Content("PDF generation failed or returned empty output.");
                    }

                    string typeName = reportType ?? "Godown";
                    string fileName = $"CommReport_{typeName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    return File(pdfBytes, "application/pdf", fileName, enableRangeProcessing: true);
                }
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult OnScreenReport(DateTime? fromDate, DateTime? toDate, string partyCode, string reportType)
        {
            int companyId = GetCompanyId();

            try
            {
                DataTable dt = GetCommReportData(fromDate, toDate, partyCode, reportType, companyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No data found for the selected filter criteria.</strong></div>", "text/html");
                }

                string compName = dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() ?? "Company" : "Company";
                string title = (reportType == "Adda") ? "COMMISSION REPORT (ADDA - commreport_all12)" : "COMMISSION REPORT (GODOWN - commreport_all1)";
                string headerBg = (reportType == "Adda") ? "#2980b9" : "#e67e22";

                decimal grandAmount = 0, grandDrAmt = 0, grandCrAmt = 0, grandDelivery = 0;
                decimal grandSTax = 0, grandDelivery2 = 0, grandPay = 0, grandQty = 0;

                var sb = new StringBuilder();
                sb.Append(@"<!DOCTYPE html><html><head><meta charset='utf-8'>
                <style>
                    body{font-family:Arial,sans-serif;font-size:11px;margin:10px;}
                    h2{text-align:center;color:#333;margin-bottom:2px;}
                    h4{text-align:center;color:#555;margin:2px 0 8px;font-weight:normal;}
                    table{width:100%;border-collapse:collapse;}
                    th{background:" + headerBg + @";color:#fff;padding:5px 4px;text-align:left;font-size:10px;white-space:nowrap;}
                    td{padding:4px;border-bottom:1px solid #ddd;font-size:10px;white-space:nowrap;}
                    tr:hover td{background:#fef5e7;}
                    tfoot td{background:#fdebd0;font-weight:bold;}
                    .num{text-align:right;}
                </style></head><body>");

                sb.Append($"<h2>{compName}</h2>");
                sb.Append($"<h2>{title}</h2>");
                sb.Append($"<h4>From: <b>{fromDate:dd-MMM-yyyy}</b> &nbsp; To: <b>{toDate:dd-MMM-yyyy}</b> &nbsp; | &nbsp; Type: <b>{reportType}</b></h4>");

                sb.Append("<table><thead><tr>");
                sb.Append("<th>#</th><th>Date</th><th>Bill Ti No</th><th>Station</th><th>Rate</th>");
                sb.Append("<th class='num'>Amount</th><th>Vehicle No</th><th class='num'>Dr Amt</th><th class='num'>Cr Amt</th>");
                sb.Append("<th class='num'>Delivery</th><th class='num'>S.Tax</th><th class='num'>Delivery2</th>");
                sb.Append("<th>Item Name</th><th>Branch</th><th>Party Code</th>");
                sb.Append("<th class='num'>Pay</th><th class='num'>Qty</th>");
                sb.Append("</tr></thead><tbody>");

                int sr = 1;
                foreach (DataRow row in dt.Rows)
                {
                    decimal amount = row["Amount"] != DBNull.Value ? Convert.ToDecimal(row["Amount"]) : 0;
                    decimal drAmt = row["DrAmt"] != DBNull.Value ? Convert.ToDecimal(row["DrAmt"]) : 0;
                    decimal crAmt = row["CrAmt"] != DBNull.Value ? Convert.ToDecimal(row["CrAmt"]) : 0;
                    decimal delivery = row["DeliveryAmt"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt"]) : 0;
                    decimal stax = row["STaxAmt"] != DBNull.Value ? Convert.ToDecimal(row["STaxAmt"]) : 0;
                    decimal delivery2 = row["DeliveryAmt2"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt2"]) : 0;
                    decimal pay = row["Pay"] != DBNull.Value ? Convert.ToDecimal(row["Pay"]) : 0;
                    decimal qty = row["Qty"] != DBNull.Value ? Convert.ToDecimal(row["Qty"]) : 0;
                    decimal rate = row["Rate"] != DBNull.Value ? Convert.ToDecimal(row["Rate"]) : 0;

                    grandAmount += amount;
                    grandDrAmt += drAmt;
                    grandCrAmt += crAmt;
                    grandDelivery += delivery;
                    grandSTax += stax;
                    grandDelivery2 += delivery2;
                    grandPay += pay;
                    grandQty += qty;

                    string voDate = row["VoDate"] != DBNull.Value ? Convert.ToDateTime(row["VoDate"]).ToString("dd-MMM-yy") : "";

                    sb.Append($"<tr>");
                    sb.Append($"<td>{sr++}</td>");
                    sb.Append($"<td>{voDate}</td>");
                    sb.Append($"<td>{row["BillTiNo"]}</td>");
                    sb.Append($"<td>{row["Station"]}</td>");
                    sb.Append($"<td class='num'>{rate:#,##0.00}</td>");
                    sb.Append($"<td class='num'>{amount:#,##0.00}</td>");
                    sb.Append($"<td>{row["VehicleNo"]}</td>");
                    sb.Append($"<td class='num'>{drAmt:#,##0.00}</td>");
                    sb.Append($"<td class='num'>{crAmt:#,##0.00}</td>");
                    sb.Append($"<td class='num'>{delivery:#,##0.00}</td>");
                    sb.Append($"<td class='num'>{stax:#,##0.00}</td>");
                    sb.Append($"<td class='num'>{delivery2:#,##0.00}</td>");
                    sb.Append($"<td>{row["IName"]}</td>");
                    sb.Append($"<td>{row["Branch"]}</td>");
                    sb.Append($"<td>{row["PartyCode"]}</td>");
                    sb.Append($"<td class='num'>{pay:#,##0.00}</td>");
                    sb.Append($"<td class='num'>{qty:#,##0}</td>");
                    sb.Append("</tr>");
                }

                sb.Append($"<tr style='background:#fdebd0;font-weight:bold'>");
                sb.Append($"<td colspan='5' style='text-align:right'>GRAND TOTAL:</td>");
                sb.Append($"<td class='num'>{grandAmount:#,##0.00}</td><td></td>");
                sb.Append($"<td class='num'>{grandDrAmt:#,##0.00}</td>");
                sb.Append($"<td class='num'>{grandCrAmt:#,##0.00}</td>");
                sb.Append($"<td class='num'>{grandDelivery:#,##0.00}</td>");
                sb.Append($"<td class='num'>{grandSTax:#,##0.00}</td>");
                sb.Append($"<td class='num'>{grandDelivery2:#,##0.00}</td>");
                sb.Append($"<td></td><td></td><td></td>");
                sb.Append($"<td class='num'>{grandPay:#,##0.00}</td>");
                sb.Append($"<td class='num'>{grandQty:#,##0}</td>");
                sb.Append("</tr>");

                sb.Append("</tbody></table></body></html>");

                return Content(sb.ToString(), "text/html");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}", "text/html");
            }
        }

        [HttpGet]
        public IActionResult ExportExcel(DateTime? fromDate, DateTime? toDate, string partyCode, string reportType)
        {
            int companyId = GetCompanyId();

            try
            {
                DataTable dt = GetCommReportData(fromDate, toDate, partyCode, reportType, companyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                var sb = new StringBuilder();

                string compName = (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company";
                string reportTitle = (reportType == "Adda") ? "COMMISSION REPORT (ADDA - commreport_all12)" : "COMMISSION REPORT (GODOWN - commreport_all1)";
                sb.AppendLine($"\"{compName}\"");
                sb.AppendLine($"\"{reportTitle}\"");
                sb.AppendLine($"\"Date From:\",\"{fromDate:dd-MM-yyyy}\",\"Date To:\",\"{toDate:dd-MM-yyyy}\",\"Type:\",\"{reportType}\"");
                sb.AppendLine();

                sb.AppendLine("Date,Bill Ti No,Station,Rate,Amount,Vehicle No,Dr Amt,Cr Amt,Delivery Amt,S.Tax Amt,Delivery Amt2,Item Name,Branch,Party Code,Pay,Qty");

                decimal totAmt = 0, totDr = 0, totCr = 0, totDel = 0, totSTax = 0, totDel2 = 0, totPay = 0, totQty = 0;

                foreach (DataRow row in dt.Rows)
                {
                    string voDate = row["VoDate"] != DBNull.Value ? Convert.ToDateTime(row["VoDate"]).ToString("dd-MM-yyyy") : "";
                    decimal amount = row["Amount"] != DBNull.Value ? Convert.ToDecimal(row["Amount"]) : 0;
                    decimal drAmt = row["DrAmt"] != DBNull.Value ? Convert.ToDecimal(row["DrAmt"]) : 0;
                    decimal crAmt = row["CrAmt"] != DBNull.Value ? Convert.ToDecimal(row["CrAmt"]) : 0;
                    decimal delivery = row["DeliveryAmt"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt"]) : 0;
                    decimal stax = row["STaxAmt"] != DBNull.Value ? Convert.ToDecimal(row["STaxAmt"]) : 0;
                    decimal delivery2 = row["DeliveryAmt2"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt2"]) : 0;
                    decimal pay = row["Pay"] != DBNull.Value ? Convert.ToDecimal(row["Pay"]) : 0;
                    decimal qty = row["Qty"] != DBNull.Value ? Convert.ToDecimal(row["Qty"]) : 0;
                    decimal rate = row["Rate"] != DBNull.Value ? Convert.ToDecimal(row["Rate"]) : 0;

                    totAmt += amount; totDr += drAmt; totCr += crAmt; totDel += delivery;
                    totSTax += stax; totDel2 += delivery2; totPay += pay; totQty += qty;

                    sb.AppendLine($"{voDate},{EscapeCsv(row["BillTiNo"]?.ToString())},{EscapeCsv(row["Station"]?.ToString())},{rate:#,##0.00},{amount:#,##0.00},{EscapeCsv(row["VehicleNo"]?.ToString())},{drAmt:#,##0.00},{crAmt:#,##0.00},{delivery:#,##0.00},{stax:#,##0.00},{delivery2:#,##0.00},{EscapeCsv(row["IName"]?.ToString())},{EscapeCsv(row["Branch"]?.ToString())},{EscapeCsv(row["PartyCode"]?.ToString())},{pay:#,##0.00},{qty:#,##0}");
                }

                sb.AppendLine($"\"TOTAL\",,,, {totAmt:#,##0.00},,{totDr:#,##0.00},{totCr:#,##0.00},{totDel:#,##0.00},{totSTax:#,##0.00},{totDel2:#,##0.00},,,,{totPay:#,##0.00},{totQty:#,##0}");

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"CommReport_{reportType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
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
