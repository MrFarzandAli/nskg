using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Reporting.NETCore;
using Nskg.Data;
using Nskg.Extensions;
using System.Data;
using System.Text;

namespace Nskg.Controllers
{
    [Authorize]
    public class ToPayBiltyReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public ToPayBiltyReportController(ApplicationDbContext context, IConfiguration config, IWebHostEnvironment env)
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
        public IActionResult Index(string fromDate, string toDate, decimal? fromBilNo, decimal? toBilNo)
        {
            ViewBag.FromDate = string.IsNullOrEmpty(fromDate) ? DateTime.Now.ToString("yyyy-MM-dd") : fromDate;
            ViewBag.ToDate = string.IsNullOrEmpty(toDate) ? DateTime.Now.ToString("yyyy-MM-dd") : toDate;
            ViewBag.FromBilNo = fromBilNo;
            ViewBag.ToBilNo = toBilNo;
            return View();
        }

        private DataTable GetToPayBiltyList(DateTime? fromDate, DateTime? toDate, decimal? fromBilNo, decimal? toBilNo, int companyId, int financialYearId)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                string query = @"
                    SET NOCOUNT ON;

                    IF OBJECT_ID('tempdb..#ToPayBills') IS NOT NULL DROP TABLE #ToPayBills;

                    SELECT
                        b.Id,
                        ISNULL(CONVERT(INT, b.BilNo), 0) AS BilNo,
                        ISNULL(CONVERT(INT, b.BillTiNo), 0) AS BillTiNo,
                        b.DocNo,
                        b.DocDate,
                        b.StationId,
                        b.CustomerId,
                        b.Fooder,
                        b.CusName,
                        b.Qty,
                        b.NetAmt
                    INTO #ToPayBills
                    FROM ISSHEAD b
                    WHERE b.CompanyId = @CompanyId
                      AND ISNULL(b.IsDeleted, 0) = 0
                      AND b.PType = 'ToPay'
                      AND (@FromDate IS NULL OR b.DocDate >= @FromDate)
                      AND (@ToDate IS NULL OR b.DocDate <= @ToDate)
                      AND (@FromBilNo IS NULL OR b.BilNo >= @FromBilNo)
                      AND (@ToBilNo IS NULL OR b.BilNo <= @ToBilNo);

                    SELECT
                        CAST(b.BilNo AS VARCHAR(50)) AS BilNo,
                        CAST(b.BillTiNo AS VARCHAR(50)) AS BillTiNo,
                        b.DocDate,
                        COALESCE(g.Name, b.Fooder, '') AS Station,
                        COALESCE(c.Name, b.CusName, '') AS PartyName,
                        b.Qty,
                        b.NetAmt AS Freight,
                        NULL AS ReceiveDate,
                        0 AS Amount,
                        b.NetAmt AS Balance
                    FROM #ToPayBills b
                    LEFT JOIN GLCHART3 g ON g.Id = b.StationId
                    LEFT JOIN GLCHART3 c ON c.Id = b.CustomerId
                    ORDER BY b.DocDate, b.BilNo, b.Id
                    OPTION (RECOMPILE);";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    cmd.Parameters.AddWithValue("@FromDate", fromDate.HasValue ? (object)fromDate.Value.Date : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ToDate", toDate.HasValue ? (object)toDate.Value.Date.AddDays(1).AddSeconds(-1) : DBNull.Value);
                    cmd.Parameters.AddWithValue("@FromBilNo", fromBilNo.HasValue && fromBilNo.Value > 0 ? (object)fromBilNo.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ToBilNo", toBilNo.HasValue && toBilNo.Value > 0 ? (object)toBilNo.Value : DBNull.Value);

                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        da.Fill(dt);
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult GeneratePDF(DateTime? fromDate, DateTime? toDate, decimal? fromBilNo, decimal? toBilNo)
        {
            int companyId = GetCompanyId();
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetToPayBiltyList(fromDate, toDate, fromBilNo, toBilNo, companyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                    return Content("No data found for the selected filter criteria.");

                string reportPath = Path.Combine(_env.WebRootPath, "Reports", "BiltyListrpt.rdlc");

                if (!System.IO.File.Exists(reportPath))
                    return Content($"Report definition file not found: {reportPath}");

                using (LocalReport report = new LocalReport())
                {
                    report.ReportPath = reportPath;
                    report.DataSources.Clear();
                    dt.TableName = "DSBiltyList";
                    report.DataSources.Add(new ReportDataSource("DSBiltyList", dt));

                    byte[] pdfBytes = report.Render("PDF");

                    if (pdfBytes == null || pdfBytes.Length < 100)
                        return Content("PDF generation failed or returned empty output.");

                    string fileName = $"ToPayBiltyList_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    return File(pdfBytes, "application/pdf", fileName, enableRangeProcessing: true);
                }
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult OnScreenReport(DateTime? fromDate, DateTime? toDate, decimal? fromBilNo, decimal? toBilNo)
        {
            int companyId = GetCompanyId();
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetToPayBiltyList(fromDate, toDate, fromBilNo, toBilNo, companyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No data found for the selected filter criteria.</strong></div>", "text/html");

                string reportPath = Path.Combine(_env.WebRootPath, "Reports", "BiltyListrpt.rdlc");

                if (!System.IO.File.Exists(reportPath))
                    return Content($"Report definition file not found: {reportPath}");

                using (LocalReport report = new LocalReport())
                {
                    report.ReportPath = reportPath;
                    report.DataSources.Clear();
                    dt.TableName = "DSBiltyList";
                    report.DataSources.Add(new ReportDataSource("DSBiltyList", dt));

                    try
                    {
                        byte[] htmlBytes = report.Render("HTML5");
                        if (htmlBytes != null && htmlBytes.Length > 0)
                            return File(htmlBytes, "text/html");
                    }
                    catch
                    {
                        // Fallback to PDF display in iframe
                    }

                    byte[] pdfBytes = report.Render("PDF");
                    return File(pdfBytes, "application/pdf");
                }
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult ExportExcel(DateTime? fromDate, DateTime? toDate, decimal? fromBilNo, decimal? toBilNo)
        {
            int companyId = GetCompanyId();
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetToPayBiltyList(fromDate, toDate, fromBilNo, toBilNo, companyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                    return Content("No data found to export.");

                var sb = new StringBuilder();
                sb.AppendLine("\"TO PAY BILTY LIST REPORT\"");
                sb.AppendLine($"\"Date From:\",\"{fromDate:dd-MM-yyyy}\",\"Date To:\",\"{toDate:dd-MM-yyyy}\",\"Bill No From:\",\"{fromBilNo}\",\"Bill No To:\",\"{toBilNo}\"");
                sb.AppendLine();
                sb.AppendLine("Bill No,Bilty No,Date,Station,Party Name,Qty,Freight,Receive Date,Amount,Balance");

                decimal totQty = 0, totFreight = 0, totAmount = 0;

                foreach (DataRow row in dt.Rows)
                {
                    string bilNo = EscapeCsv(row["BilNo"]?.ToString() ?? "");
                    string biltyNo = EscapeCsv(row["BillTiNo"]?.ToString() ?? "");
                    string docDate = row["DocDate"] != DBNull.Value && row["DocDate"] != null ? Convert.ToDateTime(row["DocDate"]).ToString("dd-MM-yyyy") : "";
                    string station = EscapeCsv(row["Station"]?.ToString() ?? "");
                    string partyName = EscapeCsv(row["PartyName"]?.ToString() ?? "");
                    decimal qty = row["Qty"] != DBNull.Value && row["Qty"] != null ? Convert.ToDecimal(row["Qty"]) : 0;
                    decimal freight = row["Freight"] != DBNull.Value && row["Freight"] != null ? Convert.ToDecimal(row["Freight"]) : 0;
                    decimal amount = 0;
                    decimal balance = freight;

                    totQty += qty; totFreight += freight; totAmount += amount;

                    sb.AppendLine($"{bilNo},{biltyNo},{docDate},{station},{partyName},{(qty != 0 ? qty.ToString("#,##0") : "")},{(freight != 0 ? freight.ToString("#,##0.00") : "")},,{(amount != 0 ? amount.ToString("#,##0.00") : "")},{balance.ToString("#,##0.00")}");
                }

                sb.AppendLine($"\"TOTAL\",,,,,{totQty:#,##0},{totFreight:#,##0.00},,{totAmount:#,##0.00},{totAmount:#,##0.00}");

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"ToPayBiltyList_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
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
