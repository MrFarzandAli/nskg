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
        public IActionResult Index(string fromDate, string toDate, decimal? fromBilNo, decimal? toBilNo, decimal? fromBiltyNo, decimal? toBiltyNo, int? companyId)
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
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.FromBilNo = fromBilNo;
            ViewBag.ToBilNo = toBilNo;
            ViewBag.FromBiltyNo = fromBiltyNo;
            ViewBag.ToBiltyNo = toBiltyNo;

            return View();
        }

        private DataTable GetBiltyList(DateTime? fromDate, DateTime? toDate, decimal? fromBilNo, decimal? toBilNo, decimal? fromBiltyNo, decimal? toBiltyNo, int companyId, int financialYearId)
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
  WHERE (b.CompanyId = @CompanyId OR @CompanyId = 0)
    AND ISNULL(b.IsDeleted,0)=0
    AND (@FromDate IS NULL OR b.DocDate >= @FromDate)
    AND (@ToDate IS NULL OR b.DocDate <= @ToDate)
    AND (@FromBilNo IS NULL OR CONVERT(INT, b.BilNo) >= @FromBilNo)
    AND (@ToBilNo IS NULL OR CONVERT(INT, b.BilNo) <= @ToBilNo)
    AND (@FromBiltyNo IS NULL OR CONVERT(INT, b.BillTiNo) >= @FromBiltyNo)
    AND (@ToBiltyNo IS NULL OR CONVERT(INT, b.BillTiNo) <= @ToBiltyNo);

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

      b.NetAmt - ISNULL(p.Amount,0) AS Balance,
      
      @CompanyName AS CompanyName,
      @FromDate AS FromDate,
      @ToDate AS ToDate,
      @FromBilNo AS FromBilNo,
      @ToBilNo AS ToBilNo

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
                    cmd.Parameters.AddWithValue("@FromBiltyNo", fromBiltyNo.HasValue && fromBiltyNo.Value > 0 ? (object)fromBiltyNo.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ToBiltyNo", toBiltyNo.HasValue && toBiltyNo.Value > 0 ? (object)toBiltyNo.Value : DBNull.Value);

                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult GeneratePDF(DateTime? fromDate, DateTime? toDate, decimal? fromBilNo, decimal? toBilNo, decimal? fromBiltyNo, decimal? toBiltyNo, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetBiltyList(fromDate, toDate, fromBilNo, toBilNo, fromBiltyNo, toBiltyNo, selectedCompanyId, financialYearId);

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
        public IActionResult OnScreenReport(DateTime? fromDate, DateTime? toDate, decimal? fromBilNo, decimal? toBilNo, decimal? fromBiltyNo, decimal? toBiltyNo, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetBiltyList(fromDate, toDate, fromBilNo, toBilNo, fromBiltyNo, toBiltyNo, selectedCompanyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No data found for the selected filter criteria.</strong></div>", "text/html");
                }

                string compName = (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company";
                decimal totQty = 0, totFreight = 0, totAmount = 0, totBalance = 0;

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
                    tfoot tr{background:#d0e2ff;font-weight:bold;}
                </style></head><body>");

                sb.Append($"<div class='header-box'>");
                sb.Append($"<h2>{compName}</h2>");
                sb.Append($"<h3>BILTY LIST REPORT</h3>");

                string filterText = "";
                if (fromDate.HasValue && toDate.HasValue) filterText += $"Date: <b>{fromDate.Value:dd-MMM-yyyy}</b> to <b>{toDate.Value:dd-MMM-yyyy}</b> ";
                if (fromBilNo.HasValue && fromBilNo > 0) filterText += $" | Bill No: <b>{fromBilNo} - {toBilNo}</b> ";
                if (fromBiltyNo.HasValue && fromBiltyNo > 0) filterText += $" | Bilty No: <b>{fromBiltyNo} - {toBiltyNo}</b>";
                if (string.IsNullOrEmpty(filterText)) filterText = "All Records";
                sb.Append($"<p>{filterText}</p>");
                sb.Append("</div>");

                sb.Append("<table><thead><tr>");
                sb.Append("<th style='width:35px;' class='center'>#</th>");
                sb.Append("<th style='width:60px;'>Bill No</th>");
                sb.Append("<th style='width:65px;'>Bilty No</th>");
                sb.Append("<th style='width:75px;'>Date</th>");
                sb.Append("<th>Station</th>");
                sb.Append("<th>Party Name</th>");
                sb.Append("<th class='num' style='width:50px;'>Qty</th>");
                sb.Append("<th class='num' style='width:80px;'>Freight</th>");
                sb.Append("<th style='width:80px;'>Rec Date</th>");
                sb.Append("<th class='num' style='width:80px;'>Amount</th>");
                sb.Append("<th class='num' style='width:85px;'>Balance</th>");
                sb.Append("</tr></thead><tbody>");

                int sr = 1;
                foreach (DataRow row in dt.Rows)
                {
                    decimal qty = row["Qty"] != DBNull.Value ? Convert.ToDecimal(row["Qty"]) : 0;
                    decimal freight = row["Freight"] != DBNull.Value ? Convert.ToDecimal(row["Freight"]) : 0;
                    decimal amount = row["Amount"] != DBNull.Value ? Convert.ToDecimal(row["Amount"]) : 0;
                    decimal balance = row["Balance"] != DBNull.Value ? Convert.ToDecimal(row["Balance"]) : 0;

                    totQty += qty;
                    totFreight += freight;
                    totAmount += amount;
                    totBalance += balance;

                    string docDate = row["DocDate"] != DBNull.Value ? Convert.ToDateTime(row["DocDate"]).ToString("dd-MMM-yy") : "";
                    string recDate = row["ReceiveDate"] != DBNull.Value ? row["ReceiveDate"].ToString() : "";

                    sb.Append("<tr>");
                    sb.Append($"<td class='center'>{sr++}</td>");
                    sb.Append($"<td class='bold'>{row["BilNo"]}</td>");
                    sb.Append($"<td>{row["BillTiNo"]}</td>");
                    sb.Append($"<td>{docDate}</td>");
                    sb.Append($"<td>{row["Station"]}</td>");
                    sb.Append($"<td>{row["PartyName"]}</td>");
                    sb.Append($"<td class='num'>{(qty != 0 ? qty.ToString("#,##0") : "")}</td>");
                    sb.Append($"<td class='num'>{(freight != 0 ? freight.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td>{recDate}</td>");
                    sb.Append($"<td class='num'>{(amount != 0 ? amount.ToString("#,##0.00") : "")}</td>");
                    sb.Append($"<td class='num bold'>{balance:#,##0.00}</td>");
                    sb.Append("</tr>");
                }

                sb.Append("</tbody><tfoot><tr>");
                sb.Append($"<td colspan='6' class='bold' style='text-align:right;'>TOTAL:</td>");
                sb.Append($"<td class='num bold'>{totQty:#,##0}</td>");
                sb.Append($"<td class='num bold'>{totFreight:#,##0.00}</td>");
                sb.Append("<td></td>");
                sb.Append($"<td class='num bold'>{totAmount:#,##0.00}</td>");
                sb.Append($"<td class='num bold'>{totBalance:#,##0.00}</td>");
                sb.Append("</tr></tfoot></table></body></html>");

                return Content(sb.ToString(), "text/html");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult ExportExcel(DateTime? fromDate, DateTime? toDate, decimal? fromBilNo, decimal? toBilNo, decimal? fromBiltyNo, decimal? toBiltyNo, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetBiltyList(fromDate, toDate, fromBilNo, toBilNo, fromBiltyNo, toBiltyNo, selectedCompanyId, financialYearId);

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
