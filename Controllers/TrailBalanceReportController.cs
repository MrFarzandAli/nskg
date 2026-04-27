using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Reporting.NETCore;
using System.Data;
using System.IO;
using System.Reflection.Emit;
using System.Text;

namespace Nskg.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TrailBalanceReportController : Controller
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public TrailBalanceReportController(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        }
        private int GetCompanyId()
        {
            return int.Parse(User.FindFirst("CompanyId")?.Value ?? "0");
        }

        private int GetFinancialYearId()
        {
            return int.Parse(User.FindFirst("FinancialYearId")?.Value ?? "0");
        }
        // GET: Display form
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        private DataTable GetTrailBalance( DateTime fromDate, DateTime toDate, int companyId, int financialYearId)
        {
            DataTable dt = new DataTable();

            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_TrialBalance", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Parameters
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId);

                    con.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader); // 🔥 Load data into DataTable
                    }
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult GeneratePDF(DateTime fromDate, DateTime toDate)
        {
            int companyId = GetCompanyId();
            int financialYearId = GetFinancialYearId();
            try
            {
                DataTable dt = GetTrailBalance(fromDate, toDate, companyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found for selected date range.");
                }

                string reportPath = Path.Combine(_env.WebRootPath, "Reports", "TrialBalancerpt.rdlc");

                if (!System.IO.File.Exists(reportPath))
                {
                    return Content($"Report file not found: {reportPath}");
                }

                using (LocalReport report = new LocalReport())
                {
                    report.ReportPath = reportPath;
                    report.DataSources.Clear();

                    // Ensure dataset name matches the RDLC dataset name used by the report.
                    dt.TableName = "DSTrailBalance";
                    report.DataSources.Add(new ReportDataSource("DSTrailBalance", dt));

                    // Use Render overload that provides warnings for better diagnostics
                    string mimeType, encoding, fileNameExtension;
                    string[] streams;
                    Microsoft.Reporting.NETCore.Warning[] warnings;
                    byte[] pdfBytes = report.Render("PDF", deviceInfo: null, out mimeType, out encoding, out fileNameExtension, out streams, out warnings);

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

                    return File(pdfBytes, "application/pdf", "TrailBalanceReport.pdf", enableRangeProcessing: true);
                }
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
            }


        [HttpGet]
        public IActionResult OnScreenReport( DateTime fromDate, DateTime toDate)
        {
            int companyId = GetCompanyId();
            int financialYearId = GetFinancialYearId();
            var dt = GetTrailBalance( fromDate, toDate, companyId, financialYearId);

            if (dt == null || dt.Rows.Count == 0)
            {
                return Content("<h4 style='color:red;'>No data found</h4>", "text/html");
            }

            string reportPath = Path.Combine(_env.WebRootPath, "Reports", "TrialBalancerpt.rdlc");

            using (LocalReport report = new LocalReport())
            {
                report.ReportPath = reportPath;

                dt.TableName = "DSTrailBalance";
                report.DataSources.Clear();
                report.DataSources.Add(new ReportDataSource("DSTrailBalance", dt));

                // HTML output for iframe
                byte[] htmlBytes = report.Render("HTML5");

                return File(htmlBytes, "text/html");
            }
        }

        [HttpGet]
        public IActionResult ExportExcel( DateTime fromDate, DateTime toDate)
        {
            int companyId = GetCompanyId();
            int financialYearId = GetFinancialYearId();
            var dt = GetTrailBalance( fromDate, toDate, companyId, financialYearId);

            if (dt == null || dt.Rows.Count == 0)
            {
                return Content("No data found for selected date range.");
            }

            var csv = ConvertDataTableToCsv(dt);
            var fileName = $"GeneralLedgerReport_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv";

            return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
        }

        private string ConvertDataTableToCsv(DataTable dt)
        {
            if (dt == null || dt.Columns.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();

            // header
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsv(dt.Columns[i].ColumnName));
            }
            sb.AppendLine();

            // rows
            foreach (DataRow row in dt.Rows)
            {
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var val = row[i] == DBNull.Value ? string.Empty : row[i].ToString();
                    sb.Append(EscapeCsv(val));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            // If contains quote, comma, or newline, wrap in quotes and escape quotes by doubling them
            if (s.Contains("\"") || s.Contains(",") || s.Contains("\n") || s.Contains("\r"))
            {
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            }
            return s;
        }
    }
}
