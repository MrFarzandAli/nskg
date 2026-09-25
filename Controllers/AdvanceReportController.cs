using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Nskg.Data;
using System.Data;
using System.Text;

namespace Nskg.Controllers
{
    [Authorize]
    public class AdvanceReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public AdvanceReportController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private int GetCompanyId(int? companyId = null)
        {
            if (companyId.HasValue && companyId.Value > 0) return companyId.Value;
            var compId = User.FindFirst("CompanyId")?.Value;
            if (int.TryParse(compId, out int id) && id > 0) return id;
            return 1006;
        }

        [HttpGet]
        public IActionResult Index(string? fromDate, string? toDate, decimal? docNo, decimal? chalNo, string? vehicleNo, string? transporter, int? companyId)
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

            ViewBag.CompanyList = companies;
            ViewBag.SelectedCompanyId = selectedCompanyId;
            ViewBag.FromDate = string.IsNullOrEmpty(fromDate) ? DateTime.Now.ToString("yyyy-MM-dd") : fromDate;
            ViewBag.ToDate = string.IsNullOrEmpty(toDate) ? DateTime.Now.ToString("yyyy-MM-dd") : toDate;
            ViewBag.DocNo = docNo;
            ViewBag.ChalNo = chalNo;
            ViewBag.VehicleNo = vehicleNo;
            ViewBag.Transporter = transporter;

            return View();
        }

        private DataTable GetAdvanceData(DateTime? fromDate, DateTime? toDate, decimal? docNo, decimal? chalNo, string? vehicleNo, string? transporter, int companyId)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            bool hasSpecificFilter = (docNo.HasValue && docNo.Value > 0)
                                  || (chalNo.HasValue && chalNo.Value > 0)
                                  || !string.IsNullOrWhiteSpace(vehicleNo)
                                  || !string.IsNullOrWhiteSpace(transporter);

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();
                string query = @"
                    SELECT
                        h.DocNo,
                        h.DocDate,
                        ISNULL(h.ChalNo, 0)         AS ChalNo,
                        ISNULL(h.Station, '')        AS Station,
                        ISNULL(h.Transporter, '')    AS Transporter,
                        ISNULL(h.VehicleNo, '')      AS VehicleNo,
                        ISNULL(h.Advance, '')        AS AdvanceAccount,
                        ISNULL(h.AdvanceCode, '')    AS AdvanceCode,
                        ISNULL(h.AdvanceAmt, 0)      AS AdvanceAmt,
                        ISNULL(h.Narration, '')      AS Narration
                    FROM CommHead h
                    WHERE h.CompanyId = @CompanyId
                      AND ISNULL(h.IsDeleted, 0) = 0
                      AND ISNULL(h.AdvanceAmt, 0) > 0";

                if (docNo.HasValue && docNo.Value > 0)
                    query += " AND h.DocNo = @DocNo";

                if (chalNo.HasValue && chalNo.Value > 0)
                    query += " AND h.ChalNo = @ChalNo";

                if (!string.IsNullOrWhiteSpace(vehicleNo))
                    query += " AND h.VehicleNo LIKE @VehicleNo";

                if (!string.IsNullOrWhiteSpace(transporter))
                    query += " AND h.Transporter LIKE @Transporter";

                if (fromDate.HasValue && !hasSpecificFilter)
                    query += " AND h.DocDate >= @FromDate";

                if (toDate.HasValue && !hasSpecificFilter)
                    query += " AND h.DocDate <= @ToDate";

                query += " ORDER BY h.DocDate DESC, h.Id DESC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);

                    if (docNo.HasValue && docNo.Value > 0)
                        cmd.Parameters.AddWithValue("@DocNo", docNo.Value);

                    if (chalNo.HasValue && chalNo.Value > 0)
                        cmd.Parameters.AddWithValue("@ChalNo", chalNo.Value);

                    if (!string.IsNullOrWhiteSpace(vehicleNo))
                        cmd.Parameters.AddWithValue("@VehicleNo", "%" + vehicleNo.Trim() + "%");

                    if (!string.IsNullOrWhiteSpace(transporter))
                        cmd.Parameters.AddWithValue("@Transporter", "%" + transporter.Trim() + "%");

                    if (fromDate.HasValue && !hasSpecificFilter)
                        cmd.Parameters.AddWithValue("@FromDate", fromDate.Value.Date);

                    if (toDate.HasValue && !hasSpecificFilter)
                        cmd.Parameters.AddWithValue("@ToDate", toDate.Value.Date.AddDays(1).AddSeconds(-1));

                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        da.Fill(dt);
                }
            }
            return dt;
        }

        [HttpGet]
        public IActionResult OnScreenReport(DateTime? fromDate, DateTime? toDate, decimal? docNo, decimal? chalNo, string? vehicleNo, string? transporter, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            try
            {
                DataTable dt = GetAdvanceData(fromDate, toDate, docNo, chalNo, vehicleNo, transporter, selectedCompanyId);

                if (dt == null || dt.Rows.Count == 0)
                    return Content("<div style='font-family:Arial;padding:30px;text-align:center;color:#721c24;background:#f8d7da;border:1px solid #f5c6cb;border-radius:6px;margin:20px'><strong>No advance data found.</strong></div>", "text/html");

                decimal grandTotal = 0;
                var sb = new StringBuilder();
                sb.Append(@"<!DOCTYPE html><html><head><meta charset='utf-8'>
                <style>
                    body{font-family:Arial,sans-serif;font-size:12px;margin:10px;}
                    h2{text-align:center;color:#333;margin-bottom:4px;}
                    p{text-align:center;color:#666;margin:2px 0 10px;}
                    table{width:100%;border-collapse:collapse;}
                    th{background:#198754;color:#fff;padding:6px 8px;text-align:left;font-size:11px;}
                    td{padding:5px 8px;border-bottom:1px solid #ddd;font-size:11px;}
                    tr:hover td{background:#f0fff4;}
                    tfoot td{background:#d1e7dd;font-weight:bold;}
                    .num{text-align:right;}
                </style></head><body>");
                sb.Append($"<h2>ADVANCE REPORT</h2>");

                string dateSubtitle = (fromDate.HasValue && toDate.HasValue)
                    ? $"From: <b>{fromDate:dd-MMM-yyyy}</b> &nbsp; To: <b>{toDate:dd-MMM-yyyy}</b>"
                    : "All Dates";
                sb.Append($"<p>{dateSubtitle}</p>");
                sb.Append("<table><thead><tr>");
                sb.Append("<th>#</th><th>Doc No</th><th>Date</th><th>Chal No</th><th>Station</th><th>Transporter</th><th>Vehicle No</th><th>Advance Account</th><th class='num'>Advance Amt</th><th>Narration</th>");
                sb.Append("</tr></thead><tbody>");

                int sr = 1;
                foreach (DataRow row in dt.Rows)
                {
                    decimal adv = row["AdvanceAmt"] != DBNull.Value ? Convert.ToDecimal(row["AdvanceAmt"]) : 0;
                    grandTotal += adv;
                    string docDate = row["DocDate"] != DBNull.Value ? Convert.ToDateTime(row["DocDate"]).ToString("dd-MMM-yy") : "";
                    sb.Append($"<tr><td>{sr++}</td><td>{row["DocNo"]}</td><td>{docDate}</td><td>{row["ChalNo"]}</td><td>{row["Station"]}</td><td>{row["Transporter"]}</td><td>{row["VehicleNo"]}</td><td>{row["AdvanceAccount"]}</td><td class='num'>{adv:#,##0.00}</td><td>{row["Narration"]}</td></tr>");
                }

                sb.Append($"<tr><td colspan='8' style='text-align:right;font-weight:bold;background:#d1e7dd'>GRAND TOTAL:</td><td class='num' style='background:#d1e7dd;font-weight:bold'>{grandTotal:#,##0.00}</td><td style='background:#d1e7dd'></td></tr>");
                sb.Append("</tbody></table></body></html>");

                return Content(sb.ToString(), "text/html");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}", "text/html");
            }
        }

        [HttpGet]
        public IActionResult GeneratePDF(DateTime? fromDate, DateTime? toDate, decimal? docNo, decimal? chalNo, string? vehicleNo, string? transporter, int? companyId)
        {
            return RedirectToAction("OnScreenReport", new { fromDate, toDate, docNo, chalNo, vehicleNo, transporter, companyId });
        }

        [HttpGet]
        public IActionResult ExportExcel(DateTime? fromDate, DateTime? toDate, decimal? docNo, decimal? chalNo, string? vehicleNo, string? transporter, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            try
            {
                DataTable dt = GetAdvanceData(fromDate, toDate, docNo, chalNo, vehicleNo, transporter, selectedCompanyId);
                if (dt == null || dt.Rows.Count == 0)
                    return Content("No data found to export.");

                var sb = new StringBuilder();
                sb.AppendLine("\"ADVANCE REPORT\"");
                sb.AppendLine($"\"Date From:\",\"{fromDate:dd-MM-yyyy}\",\"Date To:\",\"{toDate:dd-MM-yyyy}\"");
                sb.AppendLine();
                sb.AppendLine("Doc No,Date,Chal No,Station,Transporter,Vehicle No,Advance Account,Advance Amt,Narration");

                decimal grand = 0;
                foreach (DataRow row in dt.Rows)
                {
                    decimal adv = row["AdvanceAmt"] != DBNull.Value ? Convert.ToDecimal(row["AdvanceAmt"]) : 0;
                    grand += adv;
                    string docDate = row["DocDate"] != DBNull.Value ? Convert.ToDateTime(row["DocDate"]).ToString("dd-MM-yyyy") : "";
                    sb.AppendLine($"{row["DocNo"]},{docDate},{row["ChalNo"]},{EscCsv(row["Station"]?.ToString())},{EscCsv(row["Transporter"]?.ToString())},{row["VehicleNo"]},{EscCsv(row["AdvanceAccount"]?.ToString())},{adv:#,##0.00},{EscCsv(row["Narration"]?.ToString())}");
                }
                sb.AppendLine($"\"TOTAL\",,,,,,, {grand:#,##0.00},");

                return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"AdvanceReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}");
            }
        }

        private string EscCsv(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Contains(",") || text.Contains("\"") || text.Contains("\n"))
                return $"\"{text.Replace("\"", "\"\"")}\"";
            return text;
        }
    }
}
