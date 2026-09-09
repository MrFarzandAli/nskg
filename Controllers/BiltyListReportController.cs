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
    public class BiltyListReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public BiltyListReportController(ApplicationDbContext context, IConfiguration config, IWebHostEnvironment env)
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

        private DataTable GetBiltyList(DateTime? fromDate, DateTime? toDate, decimal? fromBilNo, decimal? toBilNo, int companyId, int financialYearId)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                // Fetch Company Name
                string companyName = "West Wharf-New Shadab Karachi Goods Transports";
                using (SqlCommand cmdComp = new SqlCommand("SELECT TOP 1 Name FROM Companies WHERE Id = @CompanyId", con))
                {
                    cmdComp.Parameters.AddWithValue("@CompanyId", companyId);
                    var res = cmdComp.ExecuteScalar();
                    if (res != null && res != DBNull.Value && !string.IsNullOrWhiteSpace(res.ToString()))
                        companyName = res.ToString();
                }

                string query = @"
                   SET NOCOUNT ON;

IF OBJECT_ID('tempdb..#Bills') IS NOT NULL DROP TABLE #Bills;
IF OBJECT_ID('tempdb..#Payments') IS NOT NULL DROP TABLE #Payments;

----------------------------------------------------
-- Bills
----------------------------------------------------
SELECT
    b.Id,
    CONVERT(INT, b.BilNo) AS BilNo,
    CONVERT(INT, b.BillTiNo) AS BillTiNo,
    b.DocNo,
    b.DocDate,
    b.StationId,
    b.CustomerId,
    b.Fooder,
    b.CusName,
    b.Qty,
    b.NetAmt
INTO #Bills
FROM ISSHEAD b
WHERE b.CompanyId = 1006
  AND ISNULL(b.IsDeleted,0)=0
  AND b.DocDate >= '20260101'
  AND b.DocDate < '20260802';

CREATE CLUSTERED INDEX IX_Bills
ON #Bills(BilNo, BillTiNo);

----------------------------------------------------
-- Payments
----------------------------------------------------
SELECT
    v.BilNo,
    v.BillTiNo,
    STRING_AGG(
        CONVERT(VARCHAR(10), v.VoDate, 23),
        ', '
    ) AS ReceiveDate,
    SUM(v.Cramt) AS Amount
INTO #Payments
FROM VoDet v
INNER JOIN #Bills b
    ON v.BilNo = b.BilNo
   AND v.BillTiNo = b.BillTiNo
WHERE v.IsDeleted = 0
GROUP BY
    v.BilNo,
    v.BillTiNo;

CREATE CLUSTERED INDEX IX_Payments
ON #Payments(BilNo, BillTiNo);

----------------------------------------------------
-- Final
----------------------------------------------------
SELECT
    CAST(b.BilNo AS VARCHAR(50)) AS BilNo,
    CAST(b.BillTiNo AS VARCHAR(50)) AS BillTiNo,
    b.DocDate,

    COALESCE(g.Name, b.Fooder, '') AS Station,
    COALESCE(c.Name, b.CusName, '') AS PartyName,

    b.Qty,
    b.NetAmt AS Freight,

    p.ReceiveDate,

    ISNULL(p.Amount,0) AS Amount,

    b.NetAmt - ISNULL(p.Amount,0) AS Balance

FROM #Bills b

LEFT JOIN GLCHART3 g
    ON g.Id = b.StationId

LEFT JOIN GLCHART3 c
    ON c.Id = b.CustomerId

LEFT JOIN #Payments p
    ON p.BilNo = b.BilNo
   AND p.BillTiNo = b.BillTiNo

ORDER BY
    b.DocDate,
    b.BilNo,
    b.Id
OPTION (RECOMPILE);
                ";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    cmd.Parameters.AddWithValue("@CompanyName", companyName);
                    cmd.Parameters.AddWithValue("@FromDate", fromDate.HasValue ? (object)fromDate.Value.Date : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ToDate", toDate.HasValue ? (object)toDate.Value.Date : DBNull.Value);
                    cmd.Parameters.AddWithValue("@FromBilNo", fromBilNo.HasValue && fromBilNo.Value > 0 ? (object)fromBilNo.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ToBilNo", toBilNo.HasValue && toBilNo.Value > 0 ? (object)toBilNo.Value : DBNull.Value);

                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
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
                DataTable dt = GetBiltyList(fromDate, toDate, fromBilNo, toBilNo, companyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found for the selected filter criteria.");
                }

                string reportPath = Path.Combine(_env.WebRootPath, "Reports", "BiltyListrpt.rdlc");

                if (!System.IO.File.Exists(reportPath))
                {
                    return Content($"Report definition file not found: {reportPath}");
                }

                using (LocalReport report = new LocalReport())
                {
                    report.ReportPath = reportPath;
                    report.DataSources.Clear();

                    dt.TableName = "DSBiltyList";
                    report.DataSources.Add(new ReportDataSource("DSBiltyList", dt));

                    byte[] pdfBytes = report.Render("PDF");

                    if (pdfBytes == null || pdfBytes.Length < 100)
                    {
                        return Content("PDF generation failed or returned empty output.");
                    }

                    string fileName = $"BiltyList_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
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
                DataTable dt = GetBiltyList(fromDate, toDate, fromBilNo, toBilNo, companyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No data found for the selected filter criteria.</strong></div>", "text/html");
                }

                string reportPath = Path.Combine(_env.WebRootPath, "Reports", "BiltyListrpt.rdlc");

                if (!System.IO.File.Exists(reportPath))
                {
                    return Content($"Report definition file not found: {reportPath}");
                }

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
                        {
                            return File(htmlBytes, "text/html");
                        }
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
                DataTable dt = GetBiltyList(fromDate, toDate, fromBilNo, toBilNo, companyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                var sb = new StringBuilder();

                string compName = (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company";
                sb.AppendLine($"\"{compName}\"");
                sb.AppendLine("\"BILTY LIST REPORT\"");
                sb.AppendLine($"\"Date From:\",\"{fromDate:dd-MM-yyyy}\",\"Date To:\",\"{toDate:dd-MM-yyyy}\",\"Bill No From:\",\"{fromBilNo}\",\"Bill No To:\",\"{toBilNo}\"");
                sb.AppendLine();

                // CSV Columns: bilno, biltyno, date, station, partyname, qty, frieight, recevice date, amount, balance
                sb.AppendLine("Bill No,Bilty No,Date,Station,Party Name,Qty,Freight,Receive Date,Amount,Balance");

                decimal totQty = 0;
                decimal totFreight = 0;
                decimal totAmount = 0;

                foreach (DataRow row in dt.Rows)
                {
                    string bilNo = EscapeCsv(row["BilNo"]?.ToString() ?? "");
                    string biltyNo = EscapeCsv(row["BillTiNo"]?.ToString() ?? "");
                    string docDate = row["DocDate"] != DBNull.Value && row["DocDate"] != null ? Convert.ToDateTime(row["DocDate"]).ToString("dd-MM-yyyy") : "";
                    string station = EscapeCsv(row["Station"]?.ToString() ?? "");
                    string partyName = EscapeCsv(row["PartyName"]?.ToString() ?? "");

                    decimal qty = row["Qty"] != DBNull.Value && row["Qty"] != null ? Convert.ToDecimal(row["Qty"]) : 0;
                    decimal freight = row["Freight"] != DBNull.Value && row["Freight"] != null ? Convert.ToDecimal(row["Freight"]) : 0;
                    string recDate = row["ReceiveDate"] != DBNull.Value && row["ReceiveDate"] != null ? Convert.ToDateTime(row["ReceiveDate"]).ToString("dd-MM-yyyy") : "";
                    decimal amount = row["Amount"] != DBNull.Value && row["Amount"] != null ? Convert.ToDecimal(row["Amount"]) : 0;
                    decimal balance = row["Balance"] != DBNull.Value && row["Balance"] != null ? Convert.ToDecimal(row["Balance"]) : 0;

                    totQty += qty;
                    totFreight += freight;
                    totAmount += amount;

                    string qtyStr = qty != 0 ? qty.ToString("#,##0") : "";
                    string freightStr = freight != 0 ? freight.ToString("#,##0.00") : "";
                    string amountStr = amount != 0 ? amount.ToString("#,##0.00") : "";
                    string balanceStr = balance.ToString("#,##0.00");

                    sb.AppendLine($"{bilNo},{biltyNo},{docDate},{station},{partyName},{qtyStr},{freightStr},{recDate},{amountStr},{balanceStr}");
                }

                // Total Row
                sb.AppendLine($"\"TOTAL\",,,,,{totQty:#,##0},{totFreight:#,##0.00},,{totAmount:#,##0.00},{totAmount:#,##0.00}");

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"BiltyList_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
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
