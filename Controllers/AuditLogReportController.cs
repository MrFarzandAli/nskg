using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Reporting.NETCore;
using System.Data;
using System.IO;
using System.Text;

namespace Nskg.Controllers  // Apna actual namespace dalo
{
    [Authorize(Roles = "Admin")]
    public class AuditLogReportController : Controller
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public AuditLogReportController(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        }

        // GET: Display form
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        private DataTable GetAuditLogs(DateTime fromDate, DateTime toDate, int? companyId, int? financialYearId)
        {
            DataTable dt = new DataTable();

            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_GetAuditLogs", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);
                    cmd.Parameters.AddWithValue("@CompanyId", companyId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId ?? (object)DBNull.Value);

                    con.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader); // 🔥 THIS IS THE FIX
                    }
                }
            }

            return dt;
        }

       
        [HttpGet]
        public IActionResult GeneratePDF(DateTime fromDate, DateTime toDate, int? companyId, int? financialYearId)
        {
            try
            {
                DataTable dt = GetAuditLogs(fromDate, toDate, companyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found for selected date range.");
                }

                string reportPath = Path.Combine(_env.WebRootPath, "Reports", "AuditLogrpt.rdlc");

                if (!System.IO.File.Exists(reportPath))
                {
                    return Content($"Report file not found: {reportPath}");
                }

                using (LocalReport report = new LocalReport())
                {
                    report.ReportPath = reportPath;

                    report.DataSources.Clear();

                    dt.TableName = "DSAuditLog";
                    report.DataSources.Add(new ReportDataSource("DSAuditLog", dt));

                    byte[] pdfBytes = report.Render("PDF");

                    if (pdfBytes == null || pdfBytes.Length < 1000)
                    {
                        try
                        {
                            byte[] htmlBytes = report.Render("HTML5");
                            if (htmlBytes != null && htmlBytes.Length > 0)
                            {
                                try
                                {
                                    var debugHtmlPath = Path.Combine(_env.WebRootPath, "Reports", "_last_audit_report_debug.html");
                                    System.IO.File.WriteAllBytes(debugHtmlPath, htmlBytes);
                                    return File(htmlBytes, "text/html");
                                }
                                catch
                                {
                                    return File(htmlBytes, "text/html");
                                }
                            }
                        }
                        catch
                        {
                            // ignore fallback errors
                        }

                        if (pdfBytes != null && pdfBytes.Length > 0)
                        {
                            try
                            {
                                var debugPath = Path.Combine(_env.WebRootPath, "Reports", "_last_audit_report_debug.pdf");
                                System.IO.File.WriteAllBytes(debugPath, pdfBytes);
                                return Content($"PDF generation failed or corrupted output. Debug file written to: {debugPath}");
                            }
                            catch
                            {
                                // ignore file write errors
                            }
                        }

                        return Content("PDF generation failed or corrupted output. Try opening report as HTML (fallback) or verify RDLC dataset names and resources.");
                    }

                    return File(pdfBytes, "application/pdf", "AuditLogReport.pdf", enableRangeProcessing: true);
                }
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }
        [HttpGet]
        public IActionResult ViewReport(DateTime fromDate, DateTime toDate, int? companyId, int? financialYearId)
        {
            var dt = GetAuditLogs(fromDate, toDate, companyId, financialYearId);

            ViewBag.FromDate = fromDate.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate.ToString("yyyy-MM-dd");
            ViewBag.AuditLogs = dt;
            ViewBag.Message = dt.Rows.Count == 0 ? "No data found for selected date range." : string.Empty;

            return View("Index");
        }

        [HttpGet]
        public IActionResult ExportExcel(DateTime fromDate, DateTime toDate, int? companyId, int? financialYearId)
        {
            var dt = GetAuditLogs(fromDate, toDate, companyId, financialYearId);
            if (dt.Rows.Count == 0)
            {
                return Content("No data found for selected date range.");
            }

            var csv = ConvertDataTableToCsv(dt);
            var fileName = $"AuditLogReport_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv";

            return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
        }

        private static string ConvertDataTableToCsv(DataTable dataTable)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < dataTable.Columns.Count; i++)
            {
                sb.Append(EscapeCsv(dataTable.Columns[i].ColumnName));
                if (i < dataTable.Columns.Count - 1)
                {
                    sb.Append(',');
                }
            }
            sb.AppendLine();

            foreach (DataRow row in dataTable.Rows)
            {
                for (int i = 0; i < dataTable.Columns.Count; i++)
                {
                    sb.Append(EscapeCsv(row[i]?.ToString() ?? string.Empty));
                    if (i < dataTable.Columns.Count - 1)
                    {
                        sb.Append(',');
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains('"'))
            {
                value = value.Replace("\"", "\"\"");
            }

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value}\"";
            }

            return value;
        }
    }
}