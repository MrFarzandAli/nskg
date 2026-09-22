using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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
    public class TrailBalanceReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public TrailBalanceReportController(ApplicationDbContext context, IConfiguration config, IWebHostEnvironment env)
        {
            _context = context;
            _config = config;
            _env = env;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private int GetCompanyId()
        {
            int cid = User.GetCompanyId();
            if (cid > 0) return cid;
            return 1006;
        }

        private string GetCompanyCode()
        {
            string ccode = User.GetCompanyCode();
            if (!string.IsNullOrEmpty(ccode) && ccode != "0") return ccode;
            return "01";
        }

        [HttpGet]
        public IActionResult Index(string rcocode, string fromDate, string toDate)
        {
            string defaultCoCode = GetCompanyCode();
            string selectedRco = !string.IsNullOrEmpty(rcocode) ? rcocode : defaultCoCode;

            // Load Companies
            var companies = _context.Companies
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Cocode)
                .Select(c => new
                {
                    Code = !string.IsNullOrEmpty(c.Cocode) ? c.Cocode : c.Id.ToString(),
                    Name = (!string.IsNullOrEmpty(c.Cocode) ? c.Cocode + " - " : "") + c.Name,
                    Id = c.Id
                })
                .ToList();

            ViewBag.CompanyList = companies;
            ViewBag.Rcocode = selectedRco;
            ViewBag.FromDate = string.IsNullOrEmpty(fromDate) ? DateTime.Now.ToString("yyyy-01-01") : fromDate;
            ViewBag.ToDate = string.IsNullOrEmpty(toDate) ? DateTime.Now.ToString("yyyy-MM-dd") : toDate;

            return View();
        }

        private DataTable GetTrialBalanceData(string rcocode, DateTime? fromDate, DateTime? toDate, int companyId)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                string companyName = "West Wharf-New Shadab Karachi Goods Transports";
                int targetCompanyId = companyId;

                if (!string.IsNullOrWhiteSpace(rcocode))
                {
                    using (SqlCommand cmdComp = new SqlCommand("SELECT TOP 1 Id, Name FROM Companies WHERE Cocode = @Rcocode OR CAST(Id AS NVARCHAR) = @Rcocode", con))
                    {
                        cmdComp.Parameters.AddWithValue("@Rcocode", rcocode.Trim());
                        using (var reader = cmdComp.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (reader["Id"] != DBNull.Value)
                                    targetCompanyId = Convert.ToInt32(reader["Id"]);
                                if (reader["Name"] != DBNull.Value && !string.IsNullOrWhiteSpace(reader["Name"].ToString()))
                                    companyName = reader["Name"].ToString();
                            }
                        }
                    }
                }
                else
                {
                    using (SqlCommand cmdComp = new SqlCommand("SELECT TOP 1 Name FROM Companies WHERE Id = @CompanyId", con))
                    {
                        cmdComp.Parameters.AddWithValue("@CompanyId", companyId);
                        var res = cmdComp.ExecuteScalar();
                        if (res != null && res != DBNull.Value && !string.IsNullOrWhiteSpace(res.ToString()))
                            companyName = res.ToString();
                    }
                }

                // Translated Oracle Report Builder Queries (Q_1 and Q_2):
                // Q_1: GLCHART1 where opening <> 0 and actype not in ('C') and ac1 not in ('040','074')
                // Q_2: GLCHART3 where ac1 in ('040','074') and opening <> 0
                string query = @"
                    SELECT 
                        AC1 AS Code, 
                        Name AS TitleOfAccount, 
                        CASE CoCode 
                            WHEN '01' THEN 'W.H' 
                            WHEN '02' THEN 'M.P' 
                            WHEN '03' THEN 'N.K' 
                            WHEN '04' THEN 'R.W' 
                            ELSE ISNULL(CoCode, 'W.H') 
                        END AS CompanyBranch,
                        CASE WHEN ISNULL(Opening, 0) > 0 THEN ISNULL(Opening, 0) ELSE CAST(0 AS DECIMAL(18,2)) END AS Debit,
                        CASE WHEN ISNULL(Opening, 0) < 0 THEN ABS(ISNULL(Opening, 0)) ELSE CAST(0 AS DECIMAL(18,2)) END AS Credit,
                        @CompanyName AS CompanyName
                    FROM GLChart1
                    WHERE ISNULL(Opening, 0) <> 0 
                      AND (AcType IS NULL OR AcType NOT IN ('C'))
                      AND AC1 NOT IN ('040', '074')
                      AND (
                            (@Rcocode <> '' AND (CoCode = @Rcocode OR CompanyId = @TargetCompanyId))
                            OR (@Rcocode = '' AND (CompanyId = @TargetCompanyId OR @TargetCompanyId = 0 OR CompanyId IS NULL))
                          )

                    UNION ALL

                    SELECT 
                        AC1 AS Code, 
                        Name AS TitleOfAccount, 
                        CASE CoCode 
                            WHEN '01' THEN 'W.H' 
                            WHEN '02' THEN 'M.P' 
                            WHEN '03' THEN 'N.K' 
                            WHEN '04' THEN 'R.W' 
                            ELSE ISNULL(CoCode, 'W.H') 
                        END AS CompanyBranch,
                        CASE WHEN ISNULL(Opening, 0) > 0 THEN ISNULL(Opening, 0) ELSE CAST(0 AS DECIMAL(18,2)) END AS Debit,
                        CASE WHEN ISNULL(Opening, 0) < 0 THEN ABS(ISNULL(Opening, 0)) ELSE CAST(0 AS DECIMAL(18,2)) END AS Credit,
                        @CompanyName AS CompanyName
                    FROM GLChart3
                    WHERE AC1 IN ('040', '074')
                      AND ISNULL(Opening, 0) <> 0 
                      AND (
                            (@Rcocode <> '' AND (CoCode = @Rcocode OR CompanyId = @TargetCompanyId))
                            OR (@Rcocode = '' AND (CompanyId = @TargetCompanyId OR @TargetCompanyId = 0 OR CompanyId IS NULL))
                          )

                    ORDER BY Code, TitleOfAccount;";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.CommandTimeout = 180;
                    cmd.Parameters.AddWithValue("@Rcocode", string.IsNullOrWhiteSpace(rcocode) ? "" : rcocode.Trim());
                    cmd.Parameters.AddWithValue("@TargetCompanyId", targetCompanyId);
                    cmd.Parameters.AddWithValue("@CompanyName", companyName);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult OnScreenReport(string rcocode, DateTime? fromDate, DateTime? toDate)
        {
            int companyId = GetCompanyId();

            try
            {
                DataTable dt = GetTrialBalanceData(rcocode, fromDate, toDate, companyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No records found for the selected criteria.</strong></div>", "text/html");
                }

                string companyName = dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() ?? "W. W" : "W. W";
                string branchCode = dt.Rows.Count > 0 ? dt.Rows[0]["CompanyBranch"]?.ToString() ?? "W.H" : "W.H";

                DateTime refDate = toDate ?? DateTime.Now;
                string periodText = $"For The Period of {refDate:MMMM yyyy}";
                string printDateText = $"Print Date {DateTime.Now:dd-MMM-yy HH:mm:ss}";

                decimal totalDebit = 0;
                decimal totalCredit = 0;

                var sb = new StringBuilder();
                sb.Append(@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>TRAIL BALANCE</title>
    <style>
        body { font-family: 'Courier New', Courier, monospace; margin: 20px; color: #000; font-size: 13px; }
        .report-header { text-align: left; margin-bottom: 15px; }
        .report-header h3 { margin: 0 0 4px 0; font-size: 16px; font-weight: bold; text-transform: uppercase; }
        .report-header h2 { margin: 0 0 4px 0; font-size: 18px; font-weight: bold; }
        .meta-line { display: flex; justify-content: space-between; font-weight: normal; margin-top: 4px; font-size: 12px; }
        table.report-table { width: 100%; border-collapse: collapse; margin-top: 10px; }
        table.report-table th, table.report-table td { padding: 4px 6px; font-size: 12px; }
        table.report-table thead th { border-top: 1px solid #000; border-bottom: 1px solid #000; font-weight: bold; text-align: left; }
        .text-center { text-align: center; }
        .text-left { text-align: left; }
        .text-right { text-align: right; }
        .fw-bold { font-weight: bold; }
        .grand-total-row td { border-top: 1px solid #000; border-bottom: 1px solid #000; font-weight: bold; padding-top: 6px; padding-bottom: 6px; }
        .profit-loss-row td { border-bottom: 1px solid #000; font-weight: bold; padding-top: 4px; padding-bottom: 4px; }
        @media print {
            body { margin: 0; padding: 10px; }
            .no-print { display: none !important; }
        }
    </style>
</head>
<body>");

                sb.Append($@"
    <div class='report-header'>
        <h3>{companyName}</h3>
        <h2>TRAIL BALANCE</h2>
        <div>{periodText}</div>
        <div class='meta-line'>
            <span>{printDateText}</span>
            <span>Branch: {branchCode}</span>
        </div>
    </div>

    <table class='report-table'>
        <thead>
            <tr>
                <th style='width: 80px;'>Code</th>
                <th>Title Of Account</th>
                <th style='width: 90px; text-align: center;'>Company</th>
                <th style='width: 140px; text-align: right;'>Debit</th>
                <th style='width: 140px; text-align: right;'>Credit</th>
            </tr>
        </thead>
        <tbody>");

                foreach (DataRow row in dt.Rows)
                {
                    string code = row["Code"]?.ToString() ?? "";
                    string title = row["TitleOfAccount"]?.ToString() ?? "";
                    string branch = row["CompanyBranch"]?.ToString() ?? "";
                    decimal dr = row["Debit"] != DBNull.Value ? Convert.ToDecimal(row["Debit"]) : 0;
                    decimal cr = row["Credit"] != DBNull.Value ? Convert.ToDecimal(row["Credit"]) : 0;

                    totalDebit += dr;
                    totalCredit += cr;

                    sb.Append($@"
            <tr>
                <td class='text-left'>{code}</td>
                <td class='text-left'>{title}</td>
                <td class='text-center'>{branch}</td>
                <td class='text-right'>{dr:#,##0.00}</td>
                <td class='text-right'>{cr:#,##0.00}</td>
            </tr>");
                }

                decimal diff = totalDebit - totalCredit;

                sb.Append($@"
        </tbody>
        <tfoot>
            <tr class='grand-total-row'>
                <td colspan='3' class='text-left fw-bold'>Grand Total:</td>
                <td class='text-right fw-bold'>{totalDebit:#,##0.00}</td>
                <td class='text-right fw-bold'>{totalCredit:#,##0.00}</td>
            </tr>
            <tr class='profit-loss-row'>
                <td colspan='3' class='text-left fw-bold'>Profit/Loss:</td>
                <td class='text-right fw-bold'>{diff:#,##0.00}</td>
                <td class='text-right'></td>
            </tr>
        </tfoot>
    </table>
</body>
</html>");

                return Content(sb.ToString(), "text/html");
            }
            catch (Exception ex)
            {
                return Content($"<div style='color:red; padding:20px;'><strong>Error generating Trial Balance:</strong> {ex.Message}</div>", "text/html");
            }
        }

        [HttpGet]
        public IActionResult ExportExcel(string rcocode, DateTime? fromDate, DateTime? toDate)
        {
            int companyId = GetCompanyId();

            try
            {
                DataTable dt = GetTrialBalanceData(rcocode, fromDate, toDate, companyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                var sb = new StringBuilder();
                sb.AppendLine("\"TRAIL BALANCE\"");
                sb.AppendLine($"\"For The Period:\",\"{toDate:MMMM yyyy}\",\"Company Code:\",\"{(!string.IsNullOrEmpty(rcocode) ? rcocode : "ALL")}\"");
                sb.AppendLine();
                sb.AppendLine("Code,Title Of Account,Company,Debit,Credit");

                decimal totalDr = 0;
                decimal totalCr = 0;

                foreach (DataRow row in dt.Rows)
                {
                    string code = EscapeCsv(row["Code"]?.ToString() ?? "");
                    string title = EscapeCsv(row["TitleOfAccount"]?.ToString() ?? "");
                    string branch = EscapeCsv(row["CompanyBranch"]?.ToString() ?? "");
                    decimal dr = row["Debit"] != DBNull.Value ? Convert.ToDecimal(row["Debit"]) : 0;
                    decimal cr = row["Credit"] != DBNull.Value ? Convert.ToDecimal(row["Credit"]) : 0;

                    totalDr += dr;
                    totalCr += cr;

                    sb.AppendLine($"{code},{title},{branch},{dr:#,##0.00},{cr:#,##0.00}");
                }

                sb.AppendLine($",Grand Total:,,{totalDr:#,##0.00},{totalCr:#,##0.00}");
                sb.AppendLine($",Profit/Loss:,,{(totalDr - totalCr):#,##0.00},");

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"TrailBalanceReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
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
