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
    public class VehicleListReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public VehicleListReportController(ApplicationDbContext context, IConfiguration config, IWebHostEnvironment env)
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
                .Where(x => (x.CompanyId == companyId || x.CompanyId == 0 || x.CompanyId == null))
                .Where(x => x.AC1 == "067" || x.AC1 == "068" || (x.Name != null && (x.Name.Contains("GODOWN") || x.Name.Contains("ADDA"))) || x.AC1 == "060")
                .Select(x => new
                {
                    code = (x.AC1 ?? "") + (x.AC3 ?? ""),
                    name = x.Name,
                    type = (x.AC1 == "068" || (x.Name != null && x.Name.ToUpper().Contains("GODOWN"))) ? "Godown" : "Adda"
                })
                .OrderBy(x => x.name)
                .ToList();

            if (!accounts.Any())
            {
                accounts = _context.GLChart3
                    .Where(x => (x.CompanyId == companyId || x.CompanyId == 0 || x.CompanyId == null))
                    .Select(x => new
                    {
                        code = (x.AC1 ?? "") + (x.AC3 ?? ""),
                        name = x.Name,
                        type = (x.Name != null && x.Name.ToUpper().Contains("GODOWN")) ? "Godown" : "Adda"
                    })
                    .OrderBy(x => x.name)
                    .ToList();
            }

            return Json(accounts);
        }

        [HttpGet]
        public IActionResult Index(string? fromDate, string? toDate, string? accode, int? companyId)
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
                .Where(x => (x.CompanyId == selectedCompanyId || x.CompanyId == 0 || x.CompanyId == null))
                .Where(x => x.AC1 == "067" || x.AC1 == "068" || (x.Name != null && (x.Name.Contains("GODOWN") || x.Name.Contains("ADDA"))) || x.AC1 == "060")
                .Select(x => new
                {
                    Code = (x.AC1 ?? "") + (x.AC3 ?? ""),
                    Name = x.Name,
                    Type = (x.AC1 == "068" || (x.Name != null && x.Name.ToUpper().Contains("GODOWN"))) ? "Godown" : "Adda"
                })
                .OrderBy(x => x.Name)
                .ToList();

            if (!accounts.Any())
            {
                accounts = _context.GLChart3
                    .Where(x => (x.CompanyId == selectedCompanyId || x.CompanyId == 0 || x.CompanyId == null))
                    .Select(x => new
                    {
                        Code = (x.AC1 ?? "") + (x.AC3 ?? ""),
                        Name = x.Name,
                        Type = (x.Name != null && x.Name.ToUpper().Contains("GODOWN")) ? "Godown" : "Adda"
                    })
                    .OrderBy(x => x.Name)
                    .ToList();
            }

            ViewBag.CompanyList = companies;
            ViewBag.SelectedCompanyId = selectedCompanyId;
            ViewBag.AccountList = accounts;
            ViewBag.FromDate = string.IsNullOrEmpty(fromDate) ? DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd") : fromDate;
            ViewBag.ToDate = string.IsNullOrEmpty(toDate) ? DateTime.Now.ToString("yyyy-MM-dd") : toDate;
            ViewBag.Accode = accode ?? "";

            return View();
        }

        private void RunVehicleListProcedure(SqlConnection con, int companyId, DateTime toDate, string accode)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand("dbo.PROCESSDETAIL_Station", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 180;
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    cmd.Parameters.AddWithValue("@TDATE", toDate.Date);
                    cmd.Parameters.AddWithValue("@ACCODE", accode?.Trim() ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                try
                {
                    using (SqlCommand cmd2 = new SqlCommand("dbo.PROCESSDETAIL", con))
                    {
                        cmd2.CommandType = CommandType.StoredProcedure;
                        cmd2.CommandTimeout = 180;
                        cmd2.Parameters.AddWithValue("@CompanyId", companyId);
                        cmd2.Parameters.AddWithValue("@TDATE", toDate.Date);
                        cmd2.Parameters.AddWithValue("@ACCODE", accode?.Trim() ?? "");
                        cmd2.ExecuteNonQuery();
                    }
                }
                catch
                {
                    // Fallback handled silently
                }
            }
        }

        private string ResolveReportType(string? accode, int companyId)
        {
            if (!string.IsNullOrWhiteSpace(accode))
            {
                var acc = _context.GLChart3.FirstOrDefault(x =>
                    (x.CompanyId == companyId || x.CompanyId == 0 || x.CompanyId == null) &&
                    ((x.AC1 + x.AC3) == accode.Trim() || x.ACC == accode.Trim()));

                if (acc != null)
                {
                    if (acc.AC1 == "068" || (!string.IsNullOrEmpty(acc.Name) && acc.Name.ToUpper().Contains("GODOWN")))
                    {
                        return "Godown";
                    }
                    if (acc.AC1 == "067" || (!string.IsNullOrEmpty(acc.Name) && acc.Name.ToUpper().Contains("ADDA")))
                    {
                        return "Adda";
                    }
                }
            }

            return "Godown";
        }

        private DataTable GetVehicleListData(DateTime? fromDate, DateTime? toDate, string? accode, out string resolvedReportType, int companyId)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                // 1. Run Vehicle List Procedure prior to querying
                DateTime effectiveToDate = toDate.HasValue ? toDate.Value : DateTime.Now;
                RunVehicleListProcedure(con, companyId, effectiveToDate, accode ?? "");

                // 2. Fetch Company Name
                string companyName = "West Wharf-New Shadab Karachi Goods Transports";
                using (SqlCommand cmdComp = new SqlCommand("SELECT TOP 1 Name FROM Companies WHERE Id = @CompanyId", con))
                {
                    cmdComp.Parameters.AddWithValue("@CompanyId", companyId);
                    var res = cmdComp.ExecuteScalar();
                    if (res != null && res != DBNull.Value && !string.IsNullOrWhiteSpace(res.ToString()))
                        companyName = res.ToString();
                }

                // 3. Resolve Report Type (Godown vs Adda)
                resolvedReportType = ResolveReportType(accode, companyId);

                string locationFilter = "";
                if (resolvedReportType == "Godown")
                {
                    locationFilter = " AND (h.Station LIKE '%Godown%' OR h.Station NOT LIKE '%Adda%') ";
                }
                else
                {
                    locationFilter = " AND (h.Station LIKE '%Adda%' OR h.Station NOT LIKE '%Godown%') ";
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

                    WHERE (h.CompanyId = @CompanyId OR @CompanyId = 0)
                      AND ISNULL(h.IsDeleted, 0) = 0";

                if (fromDate.HasValue)
                    query += " AND h.DocDate >= @FromDate";

                if (toDate.HasValue)
                    query += " AND h.DocDate <= @ToDate";

                if (!string.IsNullOrWhiteSpace(accode))
                {
                    query += @" AND (
                        (ISNULL(g.AC1, '') + ISNULL(g.AC3, '')) = @Accode 
                        OR g.ACC = @Accode 
                        OR h.Station LIKE '%' + @Accode + '%'
                        OR EXISTS (SELECT 1 FROM GLChart3 g2 WHERE g2.Id = h.StationId AND ((g2.AC1 + g2.AC3) = @Accode OR g2.ACC = @Accode))
                    )";
                }

                query += locationFilter;
                query += " ORDER BY h.DocDate, d.BillTiNo";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    cmd.Parameters.AddWithValue("@CompanyName", companyName);
                    cmd.Parameters.AddWithValue("@Accode", string.IsNullOrWhiteSpace(accode) ? (object)DBNull.Value : accode.Trim());
                    cmd.Parameters.AddWithValue("@ReportType", resolvedReportType);
                    cmd.Parameters.AddWithValue("@FromDate", fromDate.HasValue ? (object)fromDate.Value.Date : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ToDate", toDate.HasValue ? (object)toDate.Value.Date : DBNull.Value);

                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult GeneratePDF(DateTime? fromDate, DateTime? toDate, string? accode, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);

            try
            {
                DataTable dt = GetVehicleListData(fromDate, toDate, accode, out string resolvedType, selectedCompanyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found for the selected vehicle list parameters.");
                }

                // Godown: CommReportrpt.rdlc (commreportall_1) | Adda: CommReportAddarpt.rdlc (commreport_all12)
                string rdlcFile = (resolvedType == "Adda") ? "CommReportAddarpt.rdlc" : "CommReportrpt.rdlc";
                string reportPath = Path.Combine(_env.WebRootPath, "Reports", rdlcFile);

                if (!System.IO.File.Exists(reportPath))
                {
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

                    string fileName = $"VehicleList_{resolvedType}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    return File(pdfBytes, "application/pdf", fileName, enableRangeProcessing: true);
                }
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult OnScreenReport(DateTime? fromDate, DateTime? toDate, string? accode, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);

            try
            {
                DataTable dt = GetVehicleListData(fromDate, toDate, accode, out string resolvedType, selectedCompanyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No data found for the selected vehicle list filter criteria.</strong></div>", "text/html");
                }

                string compName = dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() ?? "Company" : "Company";
                string title = (resolvedType == "Adda") ? "VEHICLE LIST REPORT (ADDA - commreport_all12)" : "VEHICLE LIST REPORT (GODOWN - commreportall_1)";
                string headerBg = "#0d6efd";

                decimal grandAmount = 0, grandDrAmt = 0, grandCrAmt = 0, grandDelivery = 0;
                decimal grandSTax = 0, grandDelivery2 = 0, grandPay = 0, grandQty = 0;

                var sb = new StringBuilder();
                sb.Append(@"<!DOCTYPE html><html><head><meta charset='utf-8'>
                <style>
                    body{font-family:Arial,sans-serif;font-size:11px;margin:10px;color:#333;}
                    .header-box{text-align:center;margin-bottom:12px;}
                    .header-box h2{margin:0 0 4px;color:#0d6efd;font-size:18px;}
                    .header-box h3{margin:0 0 4px;font-size:14px;color:#495057;}
                    .header-box p{margin:0;font-size:11px;color:#6c757d;}
                    table{width:100%;border-collapse:collapse;margin-top:8px;}
                    th{background:" + headerBg + @";color:#fff;padding:6px 4px;text-align:left;font-size:10px;white-space:nowrap;border:1px solid #0b5ed7;}
                    td{padding:4px;border:1px solid #dee2e6;font-size:10px;white-space:nowrap;}
                    tr:nth-child(even){background:#f8f9fa;}
                    tr:hover td{background:#e7f1ff;}
                    tfoot tr{background:#d0e2ff;font-weight:bold;}
                    .num{text-align:right;}
                    .center{text-align:center;}
                    .bold{font-weight:bold;}
                </style></head><body>");

                sb.Append("<div class='header-box'>");
                sb.Append($"<h2>{compName}</h2>");
                sb.Append($"<h3>{title}</h3>");
                string periodText = (fromDate.HasValue && toDate.HasValue) ? $"Period: <b>{fromDate.Value:dd-MMM-yyyy}</b> to <b>{toDate.Value:dd-MMM-yyyy}</b>" : "All Dates";
                string accText = !string.IsNullOrWhiteSpace(accode) ? $" &nbsp;|&nbsp; Account: <b>{accode}</b>" : "";
                sb.Append($"<p>{periodText}{accText}</p>");
                sb.Append("</div>");

                sb.Append("<table><thead><tr>");
                sb.Append("<th class='center' style='width:30px;'>#</th><th>Date</th><th>Bill Ti No</th><th>Station</th><th class='num'>Rate</th>");
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

                    sb.Append("<tr>");
                    sb.Append($"<td class='center'>{sr++}</td>");
                    sb.Append($"<td>{voDate}</td>");
                    sb.Append($"<td class='bold'>{row["BillTiNo"]}</td>");
                    sb.Append($"<td>{row["Station"]}</td>");
                    sb.Append($"<td class='num'>{(rate != 0 ? rate.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='num'>{(amount != 0 ? amount.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='bold'>{row["VehicleNo"]}</td>");
                    sb.Append($"<td class='num'>{(drAmt != 0 ? drAmt.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='num'>{(crAmt != 0 ? crAmt.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='num'>{(delivery != 0 ? delivery.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='num'>{(stax != 0 ? stax.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='num'>{(delivery2 != 0 ? delivery2.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td>{row["IName"]}</td>");
                    sb.Append($"<td>{row["Branch"]}</td>");
                    sb.Append($"<td>{row["PartyCode"]}</td>");
                    sb.Append($"<td class='num'>{(pay != 0 ? pay.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='num'>{(qty != 0 ? qty.ToString("#,##0") : "")}</td>");
                    sb.Append("</tr>");
                }

                sb.Append("</tbody><tfoot><tr>");
                sb.Append("<td colspan='5' style='text-align:right;' class='bold'>GRAND TOTAL:</td>");
                sb.Append($"<td class='num bold'>{grandAmount:#,##0.00}</td><td></td>");
                sb.Append($"<td class='num bold'>{grandDrAmt:#,##0.00}</td>");
                sb.Append($"<td class='num bold'>{grandCrAmt:#,##0.00}</td>");
                sb.Append($"<td class='num bold'>{grandDelivery:#,##0.00}</td>");
                sb.Append($"<td class='num bold'>{grandSTax:#,##0.00}</td>");
                sb.Append($"<td class='num bold'>{grandDelivery2:#,##0.00}</td>");
                sb.Append("<td></td><td></td><td></td>");
                sb.Append($"<td class='num bold'>{grandPay:#,##0.00}</td>");
                sb.Append($"<td class='num bold'>{grandQty:#,##0}</td>");
                sb.Append("</tr></tfoot></table></body></html>");

                return Content(sb.ToString(), "text/html");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}", "text/html");
            }
        }

        [HttpGet]
        public IActionResult ExportExcel(DateTime? fromDate, DateTime? toDate, string? accode, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);

            try
            {
                DataTable dt = GetVehicleListData(fromDate, toDate, accode, out string resolvedType, selectedCompanyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                var sb = new StringBuilder();
                string compName = (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company";
                string reportTitle = (resolvedType == "Adda") ? "VEHICLE LIST REPORT (ADDA - commreport_all12)" : "VEHICLE LIST REPORT (GODOWN - commreportall_1)";
                sb.AppendLine($"\"{compName}\"");
                sb.AppendLine($"\"{reportTitle}\"");
                sb.AppendLine($"\"Date From:\",\"{fromDate:dd-MM-yyyy}\",\"Date To:\",\"{toDate:dd-MM-yyyy}\",\"Account:\",\"{accode}\"");
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

                    totAmt += amount;
                    totDr += drAmt;
                    totCr += crAmt;
                    totDel += delivery;
                    totSTax += stax;
                    totDel2 += delivery2;
                    totPay += pay;
                    totQty += qty;

                    sb.AppendLine($"{voDate},{EscapeCsv(row["BillTiNo"]?.ToString())},{EscapeCsv(row["Station"]?.ToString())},{row["Rate"]},{amount},{EscapeCsv(row["VehicleNo"]?.ToString())},{drAmt},{crAmt},{delivery},{stax},{delivery2},{EscapeCsv(row["IName"]?.ToString())},{EscapeCsv(row["Branch"]?.ToString())},{EscapeCsv(row["PartyCode"]?.ToString())},{pay},{qty}");
                }

                sb.AppendLine($"\"TOTAL\",,,,{totAmt},,{totDr},{totCr},{totDel},{totSTax},{totDel2},,,,{totPay},{totQty}");

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"VehicleList_{resolvedType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        private string EscapeCsv(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Contains(",") || text.Contains("\"") || text.Contains("\n") || text.Contains("\r"))
            {
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }
            return text;
        }
    }
}

