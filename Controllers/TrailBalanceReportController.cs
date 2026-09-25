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
        public IActionResult Index(string? rcocode, string? year)
        {
            string selectedRco = rcocode ?? "";
            string selectedYear = string.IsNullOrEmpty(year) ? "All" : year;

            // Load Companies with "All Companies / Branches" option
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

            // Load Available Financial Years / Yearly Closures
            var yearList = new List<string> { "All" };
            var fyList = _context.FinancialYears
                .Where(f => !f.IsDeleted && !string.IsNullOrEmpty(f.YearName))
                .OrderByDescending(f => f.StartDate)
                .Select(f => f.YearName)
                .Distinct()
                .ToList();

            var standardYears = new[] { 
                "2026-2027", "2025-2026", "2024-2025", "2023-2024", "2022-2023", 
                "2021-2022", "2020-2021", "2019-2020", "2018-2019", "2017-2018", 
                "2016-2017", "2015-2016", "2014-2015", "2013-2014", "2012-2013", 
                "2011-2012", "2010-2011" 
            };

            foreach (var fy in fyList)
            {
                if (!yearList.Contains(fy)) yearList.Add(fy);
            }
            foreach (var sy in standardYears)
            {
                if (!yearList.Contains(sy)) yearList.Add(sy);
            }

            ViewBag.CompanyList = companies;
            ViewBag.Rcocode = selectedRco;
            ViewBag.YearList = yearList;
            ViewBag.SelectedYear = selectedYear;

            return View();
        }

        private DataTable GetTrialBalanceData(string? rcocode, string? year, int companyId)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                string companyName = "West Wharf-New Shadab Karachi Goods Transports (All Branches)";
                int targetCompanyId = 0;

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

                // Parse the year parameter to get date range
                // Format: "2025-2026" means July 1, 2025 to June 30, 2026
                DateTime? yearStartDate = null;
                DateTime? yearEndDate = null;
                bool filterByYear = false;

                if (!string.IsNullOrEmpty(year) && !year.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = year.Split('-');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int startYear) && int.TryParse(parts[1], out int endYear))
                    {
                        yearStartDate = new DateTime(startYear, 7, 1);
                        yearEndDate = new DateTime(endYear, 6, 30);
                        filterByYear = true;
                    }
                }

                // Trial Balance Query:
                // Opening balances from GLChart1/GLChart3 (static Opening column)
                // + VoDet transactions summed by AC1 (filtered by year if selected)
                // The combined result gives the trial balance per account head
                string query = @"
                    ;WITH ChartAccounts AS (
                        -- Q_1: GLChart1 accounts (non-party, non-expense heads)
                        SELECT 
                            AC1 AS Code, 
                            Name AS TitleOfAccount, 
                            CoCode,
                            CompanyId,
                            ISNULL(Opening, 0) AS Opening
                        FROM GLChart1
                        WHERE (AcType IS NULL OR AcType NOT IN ('C'))
                          AND AC1 NOT IN ('040', '074')
                          AND (
                                (@Rcocode <> '' AND (CoCode = @Rcocode OR CompanyId = @TargetCompanyId))
                                OR (@Rcocode = '')
                              )

                        UNION ALL

                        -- Q_2: GLChart3 sub-accounts for party/expense heads
                        SELECT 
                            AC1 AS Code, 
                            Name AS TitleOfAccount, 
                            CoCode,
                            CompanyId,
                            ISNULL(Opening, 0) AS Opening
                        FROM GLChart3
                        WHERE AC1 IN ('040', '074')
                          AND (
                                (@Rcocode <> '' AND (CoCode = @Rcocode OR CompanyId = @TargetCompanyId))
                                OR (@Rcocode = '')
                              )
                    ),
                    VoDetTotals AS (
                        -- Sum of VoDet transactions per AC1, filtered by year if applicable
                        SELECT 
                            AC1,
                            COCODE,
                            SUM(ISNULL(DRAMT, 0)) AS TotalDr,
                            SUM(ISNULL(CRAMT, 0)) AS TotalCr
                        FROM VoDet
                        WHERE ISNULL(IsDeleted, 0) = 0
                          AND (
                                (@FilterByYear = 1 AND VODATE >= @YearStartDate AND VODATE <= @YearEndDate)
                                OR (@FilterByYear = 0)
                              )
                          AND (
                                (@Rcocode <> '' AND (COCODE = @Rcocode OR COCODE = CAST(@TargetCompanyId AS VARCHAR)))
                                OR (@Rcocode = '')
                              )
                        GROUP BY AC1, COCODE
                    ),
                    Combined AS (
                        SELECT 
                            ca.Code,
                            ca.TitleOfAccount,
                            CASE ca.CoCode 
                                WHEN '01' THEN 'W.H' 
                                WHEN '02' THEN 'M.P' 
                                WHEN '03' THEN 'N.K' 
                                WHEN '04' THEN 'R.W' 
                                ELSE ISNULL(ca.CoCode, 'W.H') 
                            END AS CompanyBranch,
                            ca.Opening + ISNULL(v.TotalDr, 0) - ISNULL(v.TotalCr, 0) AS NetBalance,
                            @CompanyName AS CompanyName
                        FROM ChartAccounts ca
                        LEFT JOIN VoDetTotals v ON v.AC1 = ca.Code AND v.COCODE = ca.CoCode
                    )
                    SELECT 
                        Code,
                        TitleOfAccount,
                        CompanyBranch,
                        CASE WHEN NetBalance > 0 THEN NetBalance ELSE CAST(0 AS DECIMAL(18,2)) END AS Debit,
                        CASE WHEN NetBalance < 0 THEN ABS(NetBalance) ELSE CAST(0 AS DECIMAL(18,2)) END AS Credit,
                        CompanyName
                    FROM Combined
                    WHERE NetBalance <> 0
                    ORDER BY Code, TitleOfAccount;";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.CommandTimeout = 180;
                    cmd.Parameters.AddWithValue("@Rcocode", string.IsNullOrWhiteSpace(rcocode) ? "" : rcocode.Trim());
                    cmd.Parameters.AddWithValue("@TargetCompanyId", targetCompanyId);
                    cmd.Parameters.AddWithValue("@CompanyName", companyName);
                    cmd.Parameters.AddWithValue("@FilterByYear", filterByYear ? 1 : 0);
                    cmd.Parameters.AddWithValue("@YearStartDate", (object?)yearStartDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@YearEndDate", (object?)yearEndDate ?? DBNull.Value);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult OnScreenReport(string? rcocode, string? year)
        {
            int companyId = GetCompanyId();

            try
            {
                DataTable dt = GetTrialBalanceData(rcocode, year, companyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No records found for the selected criteria.</strong></div>", "text/html");
                }

                string companyName = dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() ?? "W. W" : "W. W";
                string branchCode = string.IsNullOrWhiteSpace(rcocode) ? "ALL BRANCHES" : (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyBranch"]?.ToString() ?? "W.H" : "W.H");

                string selectedYearText = string.IsNullOrEmpty(year) || year.Equals("All", StringComparison.OrdinalIgnoreCase)
                    ? "Up to Current Financial Year"
                    : $"Yearly Closure: {year}";

                string periodText = $"For The Period: {selectedYearText}";
                string printDateText = $"Print Date: {DateTime.Now:dd-MMM-yy HH:mm:ss}";

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
        public IActionResult ExportExcel(string? rcocode, string? year)
        {
            int companyId = GetCompanyId();

            try
            {
                DataTable dt = GetTrialBalanceData(rcocode, year, companyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                string selectedYearText = string.IsNullOrEmpty(year) || year.Equals("All", StringComparison.OrdinalIgnoreCase)
                    ? "Up to Current Financial Year"
                    : year;

                var sb = new StringBuilder();
                sb.AppendLine("\"TRAIL BALANCE\"");
                sb.AppendLine($"\"Yearly Closure:\",\"{selectedYearText}\",\"Company Code:\",\"{(!string.IsNullOrEmpty(rcocode) ? rcocode : "ALL")}\"");
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
