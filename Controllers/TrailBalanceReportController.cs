using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
            return 0; // 0 = All Companies
        }

        private string GetCompanyCode()
        {
            string ccode = User.GetCompanyCode();
            if (!string.IsNullOrEmpty(ccode) && ccode != "0") return ccode;
            return "01";
        }

        [HttpGet]
        public IActionResult Index(string? rcocode, string? fyId)
        {
            string selectedRco = rcocode ?? "";

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

            // Load Financial Years from database only (no hardcoded years)
            var financialYears = _context.FinancialYears
                .Where(f => !f.IsDeleted && !string.IsNullOrEmpty(f.YearName))
                .OrderByDescending(f => !f.IsClosed)
                .ThenByDescending(f => f.StartDate)
                .Select(f => new
                {
                    f.Id,
                    f.YearName,
                    f.StartDate,
                    f.EndDate,
                    f.IsClosed
                })
                .ToList();

            var defaultFy = financialYears.FirstOrDefault(f => !f.IsClosed) ?? financialYears.FirstOrDefault();
            string defaultFyId = defaultFy != null ? defaultFy.Id.ToString() : "4";
            string selectedFyId = (string.IsNullOrEmpty(fyId) || fyId.Equals("All", StringComparison.OrdinalIgnoreCase)) ? defaultFyId : fyId;

            ViewBag.CompanyList = companies;
            ViewBag.Rcocode = selectedRco;
            ViewBag.FinancialYears = financialYears;
            ViewBag.SelectedFyId = selectedFyId;

            return View();
        }

        private DataTable GetTrialBalanceData(string? rcocode, string? fyId, int companyId)
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

                // Lookup FinancialYear dates from database using the selected FY Id
                DateTime? yearStartDate = null;
                DateTime? yearEndDate = null;
                int targetFyId = 4;
                if (!string.IsNullOrEmpty(fyId) && int.TryParse(fyId, out int parsedFyId))
                {
                    targetFyId = parsedFyId;
                }

                using (SqlCommand cmdFy = new SqlCommand("SELECT TOP 1 StartDate, EndDate FROM FinancialYears WHERE Id = @FyId AND IsDeleted = 0", con))
                {
                    cmdFy.Parameters.AddWithValue("@FyId", targetFyId);
                    using (var reader = cmdFy.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            yearStartDate = Convert.ToDateTime(reader["StartDate"]);
                            yearEndDate = Convert.ToDateTime(reader["EndDate"]);
                        }
                    }
                }

                if (!yearStartDate.HasValue || !yearEndDate.HasValue)
                {
                    using (SqlCommand cmdDef = new SqlCommand("SELECT TOP 1 StartDate, EndDate FROM FinancialYears WHERE IsDeleted = 0 ORDER BY CASE WHEN IsClosed = 0 THEN 0 ELSE 1 END, StartDate DESC", con))
                    {
                        using (var reader = cmdDef.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                yearStartDate = Convert.ToDateTime(reader["StartDate"]);
                                yearEndDate = Convert.ToDateTime(reader["EndDate"]);
                            }
                        }
                    }
                }

                // 1. Run sp_ProcessTrialBalance to ensure VoHead counterpart entries are synchronized with VoDet
                try
                {
                    using (SqlCommand cmdProc = new SqlCommand("dbo.sp_ProcessTrialBalance", con))
                    {
                        cmdProc.CommandType = CommandType.StoredProcedure;
                        cmdProc.CommandTimeout = 180;
                        cmdProc.Parameters.AddWithValue("@Cocode", string.IsNullOrWhiteSpace(rcocode) ? (object)DBNull.Value : rcocode.Trim());
                        cmdProc.Parameters.AddWithValue("@CompanyId", targetCompanyId > 0 ? (object)targetCompanyId : DBNull.Value);
                        cmdProc.Parameters.AddWithValue("@FinancialYearId", targetFyId > 0 ? (object)targetFyId : DBNull.Value);
                        cmdProc.Parameters.AddWithValue("@SDate", yearStartDate.HasValue ? (object)yearStartDate.Value : DBNull.Value);
                        cmdProc.Parameters.AddWithValue("@TDate", yearEndDate.HasValue ? (object)yearEndDate.Value : DBNull.Value);
                        cmdProc.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Warning: sp_ProcessTrialBalance in TrialBalance execution: " + ex.Message);
                }

                // 2. Trial Balance Query:
                // Accurately aggregates VoDet and VoHead (Cash/Bank counterpart heads) for the selected Financial Year dates
                // Eliminates duplicate joins and provides balanced debits and credits
                string query = @"
                    ;WITH Trans AS (
                        -- VoDet (all detail vouchers)
                        SELECT 
                            CASE WHEN RTRIM(AC1) IN ('040', '074') THEN RTRIM(AC1) + RTRIM(AC3) ELSE RTRIM(AC1) END AS Code,
                            RTRIM(AC1) AS AC1,
                            RTRIM(AC3) AS AC3,
                            COCODE,
                            ISNULL(dramt, 0) AS Debit,
                            ISNULL(cramt, 0) AS Credit
                        FROM VoDet
                        WHERE ISNULL(IsDeleted, 0) = 0
                          AND VODATE >= @YearStartDate AND VODATE <= @YearEndDate
                          AND (@Rcocode = '' OR (COCODE = @Rcocode OR COCODE = CAST(@TargetCompanyId AS VARCHAR)))

                        UNION ALL

                        -- VoHead (Cash / Bank accounts)
                        SELECT 
                            CASE WHEN SUBSTRING(COALESCE(NULLIF(RTRIM(haccode), ''), NULLIF(RTRIM(ac1) + RTRIM(ac3), ''), '001001'), 1, 3) IN ('040', '074') 
                                 THEN COALESCE(NULLIF(RTRIM(haccode), ''), NULLIF(RTRIM(ac1) + RTRIM(ac3), ''), '001001')
                                 ELSE SUBSTRING(COALESCE(NULLIF(RTRIM(haccode), ''), NULLIF(RTRIM(ac1) + RTRIM(ac3), ''), '001001'), 1, 3) END AS Code,
                            SUBSTRING(COALESCE(NULLIF(RTRIM(haccode), ''), NULLIF(RTRIM(ac1) + RTRIM(ac3), ''), '001001'), 1, 3) AS AC1,
                            SUBSTRING(COALESCE(NULLIF(RTRIM(haccode), ''), NULLIF(RTRIM(ac1) + RTRIM(ac3), ''), '001001'), 4, 3) AS AC3,
                            COCODE,
                            ISNULL(hdramt, 0) AS Debit,
                            ISNULL(hcramt, 0) AS Credit
                        FROM VoHead
                        WHERE ISNULL(IsDeleted, 0) = 0
                          AND votype <> 'JV'
                          AND VODATE >= @YearStartDate AND VODATE <= @YearEndDate
                          AND (@Rcocode = '' OR (COCODE = @Rcocode OR COCODE = CAST(@TargetCompanyId AS VARCHAR)))
                    ),
                    TransAgg AS (
                        SELECT 
                            Code,
                            AC1,
                            AC3,
                            COCODE,
                            SUM(Debit) AS TotalDr,
                            SUM(Credit) AS TotalCr
                        FROM Trans
                        GROUP BY Code, AC1, AC3, COCODE
                    ),
                    UniqueG3 AS (
                        SELECT 
                            RTRIM(AC1) + RTRIM(AC3) AS FullCode, 
                            MAX(Name) AS Name
                        FROM GLChart3
                        GROUP BY RTRIM(AC1) + RTRIM(AC3)
                    ),
                    UniqueG1 AS (
                        SELECT 
                            RTRIM(AC1) AS AC1, 
                            MAX(Name) AS Name
                        FROM GLChart1
                        GROUP BY RTRIM(AC1)
                    ),
                    Combined AS (
                        SELECT 
                            t.Code,
                            COALESCE(g3.Name, g1.Name, 'HEAD ' + t.Code) AS TitleOfAccount,
                            CASE t.COCODE 
                                WHEN '01' THEN 'W.H' 
                                WHEN '02' THEN 'M.P' 
                                WHEN '03' THEN 'N.K' 
                                WHEN '04' THEN 'R.W' 
                                ELSE ISNULL(t.COCODE, 'W.H') 
                            END AS CompanyBranch,
                            t.TotalDr - t.TotalCr AS NetBalance
                        FROM TransAgg t
                        LEFT JOIN UniqueG3 g3 ON g3.FullCode = t.Code
                        LEFT JOIN UniqueG1 g1 ON g1.AC1 = t.AC1
                    )
                    SELECT 
                        Code,
                        TitleOfAccount,
                        CompanyBranch,
                        CASE WHEN NetBalance > 0 THEN NetBalance ELSE 0 END AS Debit,
                        CASE WHEN NetBalance < 0 THEN ABS(NetBalance) ELSE 0 END AS Credit,
                        @CompanyName AS CompanyName
                    FROM Combined
                    WHERE NetBalance <> 0
                    ORDER BY Code, CompanyBranch;";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.CommandTimeout = 180;
                    cmd.Parameters.AddWithValue("@Rcocode", string.IsNullOrWhiteSpace(rcocode) ? "" : rcocode.Trim());
                    cmd.Parameters.AddWithValue("@TargetCompanyId", targetCompanyId);
                    cmd.Parameters.AddWithValue("@CompanyName", companyName);
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
        public IActionResult OnScreenReport(string? rcocode, string? fyId)
        {
            int companyId = GetCompanyId();

            try
            {
                DataTable dt = GetTrialBalanceData(rcocode, fyId, companyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No records found for the selected criteria.</strong></div>", "text/html");
                }

                string companyName = dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() ?? "W. W" : "W. W";
                string branchCode = string.IsNullOrWhiteSpace(rcocode) ? "ALL BRANCHES" : (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyBranch"]?.ToString() ?? "W.H" : "W.H");

                // Lookup Financial Year name and dates for display
                string selectedYearText = "All Financial Years";
                if (!string.IsNullOrEmpty(fyId) && !fyId.Equals("All", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(fyId, out int parsedFyId))
                {
                    var fy = _context.FinancialYears.FirstOrDefault(f => f.Id == parsedFyId && !f.IsDeleted);
                    if (fy != null)
                        selectedYearText = $"{fy.YearName} ({fy.StartDate:dd-MMM-yyyy} to {fy.EndDate:dd-MMM-yyyy})";
                }

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
        public IActionResult ExportExcel(string? rcocode, string? fyId)
        {
            int companyId = GetCompanyId();

            try
            {
                DataTable dt = GetTrialBalanceData(rcocode, fyId, companyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                // Lookup Financial Year name for the CSV header
                string selectedYearText = "All Financial Years";
                if (!string.IsNullOrEmpty(fyId) && !fyId.Equals("All", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(fyId, out int parsedFyId))
                {
                    var fy = _context.FinancialYears.FirstOrDefault(f => f.Id == parsedFyId && !f.IsDeleted);
                    if (fy != null)
                        selectedYearText = $"{fy.YearName} ({fy.StartDate:dd-MMM-yyyy} to {fy.EndDate:dd-MMM-yyyy})";
                }

                var sb = new StringBuilder();
                sb.AppendLine("\"TRAIL BALANCE\"");
                sb.AppendLine($"\"Financial Year:\",\"{selectedYearText}\",\"Company Code:\",\"{(!string.IsNullOrEmpty(rcocode) ? rcocode : "ALL")}\"");
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

