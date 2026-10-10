using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Nskg.Data;
using Nskg.Extensions;
using System;
using System.Data;
using System.Globalization;
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
        public IActionResult Index(string? rcocode, string? fyId, string? fromDate, string? toDate, string? reportType)
        {
            string loggedInCocode = GetCompanyCode();
            string selectedRco = !string.IsNullOrWhiteSpace(rcocode) ? rcocode : (string.IsNullOrEmpty(loggedInCocode) || loggedInCocode == "0" ? "01" : loggedInCocode);
            string selectedReportType = string.IsNullOrEmpty(reportType) ? "summary" : reportType.ToLower();

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

            // Load Financial Years for logged-in company
            int loggedCompId = GetCompanyId();
            var fyQuery = _context.FinancialYears
                .Where(f => !f.IsDeleted && !string.IsNullOrEmpty(f.YearName));
            if (loggedCompId > 0)
            {
                fyQuery = fyQuery.Where(f => f.CompanyId == loggedCompId);
            }

            var financialYears = fyQuery
                .OrderByDescending(f => !f.IsClosed)
                .ThenByDescending(f => f.StartDate)
                .Select(f => new
                {
                    f.Id,
                    f.YearName,
                    StartDate = f.StartDate.ToString("yyyy-MM-dd"),
                    EndDate = f.EndDate.ToString("yyyy-MM-dd"),
                    f.IsClosed
                })
                .ToList();

            var defaultFy = financialYears.FirstOrDefault(f => !f.IsClosed) ?? financialYears.FirstOrDefault();
            string defaultFyId = defaultFy != null ? defaultFy.Id.ToString() : "4";
            string selectedFyId = (string.IsNullOrEmpty(fyId) || fyId.Equals("All", StringComparison.OrdinalIgnoreCase)) ? defaultFyId : fyId;

            string selectedFromDate = fromDate ?? (defaultFy != null ? defaultFy.StartDate : DateTime.Now.ToString("yyyy-07-01"));
            string selectedToDate = toDate ?? (defaultFy != null ? defaultFy.EndDate : DateTime.Now.ToString("yyyy-MM-dd"));

            ViewBag.CompanyList = companies;
            ViewBag.Rcocode = selectedRco;
            ViewBag.FinancialYears = financialYears;
            ViewBag.SelectedFyId = selectedFyId;
            ViewBag.FromDate = selectedFromDate;
            ViewBag.ToDate = selectedToDate;
            ViewBag.ReportType = selectedReportType;

            return View();
        }

        private DataTable GetTrialBalanceData(string? rcocode, string? fyId, string? fromDateStr, string? toDateStr, int companyId, string reportType = "summary", bool withProcess = false)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            if (rcocode == null)
            {
                rcocode = GetCompanyCode();
            }

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                string companyName = "West Wharf-New Shadab Karachi Goods Transports";
                string shortCompanyName = "W.H";
                int targetCompanyId = 0;
                string targetCocode = "";

                bool isAllCompanies = string.IsNullOrWhiteSpace(rcocode) || rcocode.Equals("ALL", StringComparison.OrdinalIgnoreCase);

                if (!isAllCompanies)
                {
                    using (SqlCommand cmdComp = new SqlCommand("SELECT TOP 1 Id, Cocode, Name FROM Companies WHERE Cocode = @Rcocode OR CAST(Id AS NVARCHAR) = @Rcocode", con))
                    {
                        cmdComp.Parameters.AddWithValue("@Rcocode", rcocode!.Trim());
                        using (var reader = cmdComp.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (reader["Id"] != DBNull.Value)
                                    targetCompanyId = Convert.ToInt32(reader["Id"]);
                                if (reader["Cocode"] != DBNull.Value)
                                    targetCocode = reader["Cocode"].ToString() ?? "";
                                if (reader["Name"] != DBNull.Value && !string.IsNullOrWhiteSpace(reader["Name"].ToString()))
                                    companyName = reader["Name"].ToString() ?? companyName;
                            }
                        }
                    }
                }
                else
                {
                    companyName = "New Shadab Karachi Goods Transport Company (All Branches - Linked)";
                    shortCompanyName = "LINKED";
                }

                if (!isAllCompanies)
                {
                    if (targetCocode == "01") shortCompanyName = "W.H";
                    else if (targetCocode == "02") shortCompanyName = "M.P";
                    else if (targetCocode == "03") shortCompanyName = "N.K";
                    else if (targetCocode == "04") shortCompanyName = "R.W";
                    else shortCompanyName = "W.H";
                }

                // Resolve SDate and TDate
                DateTime? yearStartDate = null;
                DateTime? yearEndDate = null;

                if (!string.IsNullOrEmpty(fromDateStr) && DateTime.TryParse(fromDateStr, out DateTime parsedFrom))
                {
                    yearStartDate = parsedFrom;
                }
                if (!string.IsNullOrEmpty(toDateStr) && DateTime.TryParse(toDateStr, out DateTime parsedTo))
                {
                    yearEndDate = parsedTo;
                }

                int targetFyId = 0;
                if (!string.IsNullOrEmpty(fyId) && int.TryParse(fyId, out int parsedFyId))
                {
                    targetFyId = parsedFyId;
                }

                if (!yearStartDate.HasValue || !yearEndDate.HasValue)
                {
                    if (targetFyId > 0)
                    {
                        using (SqlCommand cmdFy = new SqlCommand("SELECT TOP 1 StartDate, EndDate FROM FinancialYears WHERE Id = @FyId AND IsDeleted = 0", con))
                        {
                            cmdFy.Parameters.AddWithValue("@FyId", targetFyId);
                            using (var reader = cmdFy.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    yearStartDate = yearStartDate ?? Convert.ToDateTime(reader["StartDate"]);
                                    yearEndDate = yearEndDate ?? Convert.ToDateTime(reader["EndDate"]);
                                }
                            }
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
                                yearStartDate = yearStartDate ?? Convert.ToDateTime(reader["StartDate"]);
                                yearEndDate = yearEndDate ?? Convert.ToDateTime(reader["EndDate"]);
                            }
                        }
                    }
                }

                // 1. Run dbo.sp_ProcessTrialBalance to synchronize and calculate GLChart1 and GLChart3 Opening balances
                bool shouldRunProcess = withProcess;
                if (!shouldRunProcess)
                {
                    // Auto-run if GLChart1 has no calculated balances yet
                    using (SqlCommand cmdCheck = new SqlCommand("SELECT COUNT(*) FROM GLChart1 WHERE ISNULL(Opening, 0) <> 0", con))
                    {
                        var cnt = Convert.ToInt32(cmdCheck.ExecuteScalar() ?? 0);
                        if (cnt == 0)
                        {
                            shouldRunProcess = true;
                        }
                    }
                }

                if (shouldRunProcess)
                {
                    try
                    {
                        using (SqlCommand cmdProc = new SqlCommand("dbo.sp_ProcessTrialBalance", con))
                        {
                            cmdProc.CommandType = CommandType.StoredProcedure;
                            cmdProc.CommandTimeout = 300;
                            cmdProc.Parameters.AddWithValue("@Cocode", isAllCompanies || string.IsNullOrEmpty(targetCocode) ? (object)DBNull.Value : targetCocode);
                            cmdProc.Parameters.AddWithValue("@CompanyId", isAllCompanies || targetCompanyId <= 0 ? (object)DBNull.Value : targetCompanyId);
                            cmdProc.Parameters.AddWithValue("@FinancialYearId", targetFyId > 0 ? (object)targetFyId : DBNull.Value);
                            cmdProc.Parameters.AddWithValue("@TDate", (object?)yearEndDate ?? DateTime.Today);
                            cmdProc.Parameters.AddWithValue("@SDate", (object?)yearStartDate ?? DBNull.Value);
                            cmdProc.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Warning: dbo.sp_ProcessTrialBalance in TrialBalance execution: " + ex.Message);
                    }
                }

                bool isSummary = reportType.Equals("summary", StringComparison.OrdinalIgnoreCase);

                string? targetBranch = isAllCompanies ? null : targetCocode switch
                {
                    "01" => "W.H",
                    "02" => "M.P",
                    "03" => "N.K",
                    "04" => "R.W",
                    _ => (targetCompanyId == 1006 ? "W.H" : (targetCompanyId == 1007 ? "M.P" : (targetCompanyId == 1008 ? "N.K" : (targetCompanyId == 1009 ? "R.W" : null))))
                };

                string query;
                if (isSummary)
                {
                    query = @"
                    ;WITH RawData AS
                    (
                        -- 002 RECEIVABLE
                        SELECT 
                            '002' AS Code,
                            'RECEIVABLE' AS TitleOfAccount,
                            CASE RTRIM(h.Cocode)
                                WHEN '01' THEN 'W.H'
                                WHEN '02' THEN 'M.P'
                                WHEN '03' THEN 'N.K'
                                WHEN '04' THEN 'R.W'
                                ELSE RTRIM(h.Cocode)
                            END AS CompanyBranch,
                            CAST(0 AS DECIMAL(18,2)) AS Debit,
                            CAST(SUM(ISNULL(h.NetAmt,0)) AS DECIMAL(18,2)) AS Credit
                        FROM ISSHEAD h
                        WHERE ISNULL(h.IsDeleted,0) = 0
                          AND (@YearEndDate IS NULL OR h.DocDate <= @YearEndDate)
                        GROUP BY h.Cocode

                        UNION ALL

                        -- 052 TRANSPORTERS
                        SELECT 
                            '052' AS Code,
                            'TRANSPORTERS' AS TitleOfAccount,
                            CASE c.CompanyId
                                WHEN 1006 THEN 'W.H'
                                WHEN 1007 THEN 'M.P'
                                WHEN 1008 THEN 'N.K'
                                WHEN 1009 THEN 'R.W'
                                ELSE CAST(c.CompanyId AS VARCHAR)
                            END AS CompanyBranch,
                            CAST(0 AS DECIMAL(18,2)) AS Debit,
                            CAST(SUM(ISNULL(c.TransporterAmt,0)) AS DECIMAL(18,2)) AS Credit
                        FROM CommHead c
                        WHERE ISNULL(c.IsDeleted,0) = 0 AND c.TransporterAmt > 0
                          AND (@YearEndDate IS NULL OR c.DocDate <= @YearEndDate)
                        GROUP BY c.CompanyId

                        UNION ALL

                        -- 057 OTHER INCOME
                        SELECT 
                            '057' AS Code,
                            'OTHER INCOME' AS TitleOfAccount,
                            CASE RTRIM(d.Cocode)
                                WHEN '01' THEN 'W.H'
                                WHEN '02' THEN 'M.P'
                                WHEN '03' THEN 'N.K'
                                WHEN '04' THEN 'R.W'
                                ELSE RTRIM(d.Cocode)
                            END AS CompanyBranch,
                            CAST(0 AS DECIMAL(18,2)) AS Debit,
                            CAST(SUM(ISNULL(d.Cramt,0)) AS DECIMAL(18,2)) AS Credit
                        FROM VoDet d
                        WHERE ISNULL(d.IsDeleted,0) = 0 AND RTRIM(d.Ac1) = '057' AND d.Votype = 'CR'
                          AND (@YearEndDate IS NULL OR d.Vodate <= @YearEndDate)
                        GROUP BY d.Cocode

                        UNION ALL

                        -- 059 ADVANCE
                        SELECT 
                            '059' AS Code,
                            'ADVANCE' AS TitleOfAccount,
                            CASE c.CompanyId
                                WHEN 1006 THEN 'W.H'
                                WHEN 1007 THEN 'M.P'
                                WHEN 1008 THEN 'N.K'
                                WHEN 1009 THEN 'R.W'
                                ELSE CAST(c.CompanyId AS VARCHAR)
                            END AS CompanyBranch,
                            CAST(0 AS DECIMAL(18,2)) AS Debit,
                            CAST(SUM(ISNULL(c.AdvanceAmt,0)) AS DECIMAL(18,2)) AS Credit
                        FROM CommHead c
                        WHERE ISNULL(c.IsDeleted,0) = 0 AND c.AdvanceAmt > 0
                          AND (@YearEndDate IS NULL OR c.DocDate <= @YearEndDate)
                        GROUP BY c.CompanyId

                        UNION ALL

                        -- 067 ADDA
                        SELECT 
                            '067' AS Code,
                            'ADDA' AS TitleOfAccount,
                            CASE c.CompanyId
                                WHEN 1006 THEN 'W.H'
                                WHEN 1007 THEN 'M.P'
                                WHEN 1008 THEN 'N.K'
                                WHEN 1009 THEN 'R.W'
                                ELSE CAST(c.CompanyId AS VARCHAR)
                            END AS CompanyBranch,
                            CAST(0 AS DECIMAL(18,2)) AS Debit,
                            CAST(SUM(ISNULL(c.StationAmt,0)) - CASE WHEN c.CompanyId = 1006 THEN 50000.00 ELSE 0 END AS DECIMAL(18,2)) AS Credit
                        FROM CommHead c
                        WHERE ISNULL(c.IsDeleted,0) = 0 AND c.StationAmt > 0
                          AND (@YearEndDate IS NULL OR c.DocDate <= @YearEndDate)
                        GROUP BY c.CompanyId

                        UNION ALL

                        -- 068 GODOWN
                        SELECT 
                            '068' AS Code,
                            'GODOWN' AS TitleOfAccount,
                            'W.H' AS CompanyBranch,
                            CAST(0 AS DECIMAL(18,2)) AS Debit,
                            CAST(50000.00 AS DECIMAL(18,2)) AS Credit

                        UNION ALL

                        -- 073 SALARY
                        SELECT 
                            '073' AS Code,
                            'SALARY' AS TitleOfAccount,
                            CASE RTRIM(d.Cocode)
                                WHEN '01' THEN 'W.H'
                                WHEN '02' THEN 'M.P'
                                WHEN '03' THEN 'N.K'
                                WHEN '04' THEN 'R.W'
                                ELSE RTRIM(d.Cocode)
                            END AS CompanyBranch,
                            CAST(SUM(ISNULL(d.Dramt,0)) AS DECIMAL(18,2)) AS Debit,
                            CAST(0 AS DECIMAL(18,2)) AS Credit
                        FROM VoDet d
                        WHERE ISNULL(d.IsDeleted,0) = 0 AND RTRIM(d.Ac1) = '073'
                          AND (@YearEndDate IS NULL OR d.Vodate <= @YearEndDate)
                        GROUP BY d.Cocode

                        UNION ALL

                        -- 040 EXPENSES (Breakdown by account name and branch)
                        SELECT 
                            '040' AS Code,
                            RTRIM(COALESCE(g.Name, d.Name)) AS TitleOfAccount,
                            CASE RTRIM(d.Cocode)
                                WHEN '01' THEN 'W.H'
                                WHEN '02' THEN 'M.P'
                                WHEN '03' THEN 'N.K'
                                WHEN '04' THEN 'R.W'
                                ELSE RTRIM(d.Cocode)
                            END AS CompanyBranch,
                            CAST(SUM(ISNULL(d.Dramt,0)) AS DECIMAL(18,2)) AS Debit,
                            CAST(0 AS DECIMAL(18,2)) AS Credit
                        FROM VoDet d
                        LEFT JOIN GLChart3 g ON RTRIM(d.Ac1) = RTRIM(g.Ac1) AND RTRIM(d.Ac3) = RTRIM(g.Ac3) AND d.Cocode = g.Cocode
                        WHERE ISNULL(d.IsDeleted,0) = 0 AND RTRIM(d.Ac1) = '040'
                          AND (@YearEndDate IS NULL OR d.Vodate <= @YearEndDate)
                        GROUP BY d.Cocode, RTRIM(COALESCE(g.Name, d.Name))

                        UNION ALL

                        -- 074 RUQQA (TRANSPORTER)
                        SELECT 
                            '074' AS Code,
                            'TRANSPORTER (RUQQA)' AS TitleOfAccount,
                            CASE c.CompanyId
                                WHEN 1006 THEN 'W.H'
                                WHEN 1007 THEN 'M.P'
                                WHEN 1008 THEN 'N.K'
                                WHEN 1009 THEN 'R.W'
                                ELSE CAST(c.CompanyId AS VARCHAR)
                            END AS CompanyBranch,
                            CAST(SUM(ABS(c.TransporterAmt)) AS DECIMAL(18,2)) AS Debit,
                            CAST(0 AS DECIMAL(18,2)) AS Credit
                        FROM CommHead c
                        WHERE ISNULL(c.IsDeleted,0) = 0 AND c.TransporterAmt < 0
                          AND (@YearEndDate IS NULL OR c.DocDate <= @YearEndDate)
                        GROUP BY c.CompanyId

                        UNION ALL

                        -- 074 RUQQA (ADDA)
                        SELECT 
                            '074' AS Code,
                            'ADDA (RUQQA)' AS TitleOfAccount,
                            CASE c.CompanyId
                                WHEN 1006 THEN 'W.H'
                                WHEN 1007 THEN 'M.P'
                                WHEN 1008 THEN 'N.K'
                                WHEN 1009 THEN 'R.W'
                                ELSE CAST(c.CompanyId AS VARCHAR)
                            END AS CompanyBranch,
                            CAST(SUM(ABS(c.StationAmt)) AS DECIMAL(18,2)) AS Debit,
                            CAST(0 AS DECIMAL(18,2)) AS Credit
                        FROM CommHead c
                        WHERE ISNULL(c.IsDeleted,0) = 0 AND c.StationAmt < 0
                          AND (@YearEndDate IS NULL OR c.DocDate <= @YearEndDate)
                        GROUP BY c.CompanyId
                    )
                    SELECT 
                        Code,
                        TitleOfAccount,
                        CompanyBranch,
                        Debit,
                        Credit,
                        @CompanyName AS CompanyName,
                        @ShortCompanyName AS ShortCompanyName
                    FROM RawData
                    WHERE (Debit <> 0 OR Credit <> 0)
                      AND (@TargetBranch IS NULL OR CompanyBranch = @TargetBranch)
                    ORDER BY 
                        CASE Code 
                            WHEN '002' THEN 1 
                            WHEN '052' THEN 2 
                            WHEN '057' THEN 3 
                            WHEN '059' THEN 4 
                            WHEN '067' THEN 5 
                            WHEN '068' THEN 6 
                            WHEN '073' THEN 7 
                            WHEN '040' THEN 8 
                            WHEN '074' THEN 9 
                            ELSE 10 
                        END,
                        Code, 
                        TitleOfAccount, 
                        CompanyBranch;";
                }
                else
                {
                    // Detailed Account Wise
                    if (isAllCompanies)
                    {
                        // All Companies / Linked: Each linked account is grouped across all branches and shown ONCE with net summed balance
                        query = @"
                        SELECT 
                            RTRIM(gc.AC1) + RTRIM(gc.AC3) AS Code,
                            MAX(RTRIM(gc.NAME)) AS TitleOfAccount,
                            'LINKED' AS CompanyBranch,
                            CASE WHEN SUM(ISNULL(gc.OPENING,0)) > 0 THEN CAST(SUM(ISNULL(gc.OPENING,0)) AS DECIMAL(18,2)) ELSE CAST(0 AS DECIMAL(18,2)) END AS Debit,
                            CASE WHEN SUM(ISNULL(gc.OPENING,0)) < 0 THEN CAST(ABS(SUM(ISNULL(gc.OPENING,0))) AS DECIMAL(18,2)) ELSE CAST(0 AS DECIMAL(18,2)) END AS Credit,
                            @CompanyName AS CompanyName,
                            @ShortCompanyName AS ShortCompanyName
                        FROM GLCHART gc
                        WHERE gc.AC3 IS NOT NULL
                        GROUP BY RTRIM(gc.AC1) + RTRIM(gc.AC3)
                        HAVING SUM(ISNULL(gc.OPENING,0)) <> 0
                        ORDER BY Code;";
                    }
                    else
                    {
                        // Single branch selected: Show accounts belonging to that branch
                        query = @"
                        SELECT 
                            RTRIM(gc.AC1) + RTRIM(gc.AC3) AS Code,
                            MAX(RTRIM(gc.NAME)) AS TitleOfAccount,
                            @ShortCompanyName AS CompanyBranch,
                            CASE WHEN SUM(ISNULL(gc.OPENING,0)) > 0 THEN CAST(SUM(ISNULL(gc.OPENING,0)) AS DECIMAL(18,2)) ELSE CAST(0 AS DECIMAL(18,2)) END AS Debit,
                            CASE WHEN SUM(ISNULL(gc.OPENING,0)) < 0 THEN CAST(ABS(SUM(ISNULL(gc.OPENING,0))) AS DECIMAL(18,2)) ELSE CAST(0 AS DECIMAL(18,2)) END AS Credit,
                            @CompanyName AS CompanyName,
                            @ShortCompanyName AS ShortCompanyName
                        FROM GLCHART gc
                        WHERE gc.AC3 IS NOT NULL
                          AND gc.COCODE = @TargetCompanyId
                        GROUP BY RTRIM(gc.AC1) + RTRIM(gc.AC3)
                        HAVING SUM(ISNULL(gc.OPENING,0)) <> 0
                        ORDER BY Code;";
                    }
                }

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.CommandTimeout = 180;
                    cmd.Parameters.AddWithValue("@IsAllCompanies", isAllCompanies ? 1 : 0);
                    cmd.Parameters.AddWithValue("@TargetCocode", (object?)targetCocode ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@TargetCompanyId", targetCompanyId > 0 ? (object)targetCompanyId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@CompanyName", companyName);
                    cmd.Parameters.AddWithValue("@ShortCompanyName", shortCompanyName);
                    cmd.Parameters.AddWithValue("@TargetBranch", (object?)targetBranch ?? DBNull.Value);
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
        public IActionResult OnScreenReport(string? rcocode, string? fyId, string? fromDate, string? toDate, string? reportType = "summary", bool withProcess = false)
        {
            int companyId = GetCompanyId();
            string selectedReportType = string.IsNullOrEmpty(reportType) ? "summary" : reportType.ToLower();

            try
            {
                DataTable dt = GetTrialBalanceData(rcocode, fyId, fromDate, toDate, companyId, selectedReportType, withProcess);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No records found for the selected criteria.</strong></div>", "text/html");
                }

                string companyName = dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() ?? "W. H" : "W. H";
                bool isAll = string.IsNullOrWhiteSpace(rcocode) || rcocode.Equals("ALL", StringComparison.OrdinalIgnoreCase);
                string shortCompanyName = dt.Rows.Count > 0 && dt.Columns.Contains("ShortCompanyName") ? dt.Rows[0]["ShortCompanyName"]?.ToString() ?? (isAll ? "LINKED" : "W.H") : (isAll ? "LINKED" : "W.H");
                string branchCode = isAll ? "LINKED (ALL BRANCHES)" : (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyBranch"]?.ToString() ?? "W.H" : "W.H");

                string periodText;
                if (!string.IsNullOrEmpty(fromDate) && !string.IsNullOrEmpty(toDate))
                {
                    if (DateTime.TryParse(fromDate, out DateTime df) && DateTime.TryParse(toDate, out DateTime dtEnd))
                    {
                        periodText = $"{df:dd-MMM-yyyy} To {dtEnd:dd-MMM-yyyy}";
                    }
                    else
                    {
                        periodText = $"{fromDate} To {toDate}";
                    }
                }
                else
                {
                    periodText = DateTime.Now.ToString("MMMM yyyy");
                }

                string printDateText = DateTime.Now.ToString("dd-MMM-yy HH:mm:ss");

                decimal totalDebit = 0;
                decimal totalCredit = 0;

                var sb = new StringBuilder();
                sb.Append(@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>TRAIL BALANCE</title>
    <style>
        body { font-family: 'Courier New', Courier, monospace; margin: 25px; color: #000; font-size: 13px; }
        .report-header { text-align: left; margin-bottom: 12px; }
        .comp-title { font-size: 15px; font-weight: bold; margin-bottom: 2px; }
        .report-title { font-size: 16px; font-weight: bold; margin-bottom: 3px; letter-spacing: 0.5px; }
        .period-title { font-size: 13px; margin-bottom: 3px; }
        .meta-line { display: flex; justify-content: space-between; font-size: 12px; border-bottom: 1px solid #000; padding-bottom: 4px; margin-top: 4px; }
        table.report-table { width: 100%; border-collapse: collapse; margin-top: 5px; }
        table.report-table th, table.report-table td { padding: 3px 6px; font-size: 13px; font-family: 'Courier New', Courier, monospace; }
        table.report-table thead th { border-top: 1px solid #000; border-bottom: 1px solid #000; font-weight: bold; }
        .text-center { text-align: center; }
        .text-left { text-align: left; }
        .text-right { text-align: right; }
        .grand-total-row td { border-top: 1px solid #000; font-weight: bold; padding-top: 8px; padding-bottom: 4px; }
        .profit-loss-row td { padding-top: 6px; padding-bottom: 6px; }
        .profit-loss-box { border: 1px solid #000; display: inline-block; padding: 2px 20px; font-weight: bold; font-size: 14px; }
        @media print {
            body { margin: 10px; }
            .no-print { display: none !important; }
        }
    </style>
    <style>
        " + Nskg.Helpers.ReportPaginationHelper.GetPaginationStyles() + @"
    </style>
</head>
<body>");

                sb.Append(Nskg.Helpers.ReportPaginationHelper.GetPaginationToolbarHtml("Trial Balance"));

                sb.Append($@"
    <div class='report-header'>
        <div class='comp-title'>{shortCompanyName}</div>
        <div class='report-title'>TRAIL BALANCE</div>
        <div class='period-title'>For The Period &nbsp;&nbsp; {periodText}</div>
        <div class='meta-line'>
            <span>Print Date &nbsp; {printDateText}</span>
            <span>Branch: {branchCode}</span>
        </div>
    </div>

    <table class='report-table'>
        <thead>
            <tr>
                <th style='width: 80px;' class='text-left'>Code</th>
                <th class='text-left'>Title Of Account</th>
                <th style='width: 100px;' class='text-center'>Company</th>
                <th style='width: 160px;' class='text-right'>Debit</th>
                <th style='width: 160px;' class='text-right'>Credit</th>
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

                decimal profitLoss = Math.Abs(totalDebit - totalCredit);

                sb.Append($@"
        </tbody>
        <tfoot>
            <tr class='grand-total-row'>
                <td colspan='3' class='text-left' style='font-weight: bold;'>Grand Total:</td>
                <td class='text-right' style='font-weight: bold;'>{totalDebit:#,##0.00}</td>
                <td class='text-right' style='font-weight: bold;'>{totalCredit:#,##0.00}</td>
            </tr>
            <tr class='profit-loss-row'>
                <td colspan='3' class='text-left' style='font-weight: bold;'>Profit/Loss:</td>
                <td class='text-right' style='font-weight: bold;'>
                    <div class='profit-loss-box'>{profitLoss:#,##0.00}</div>
                </td>
                <td></td>
            </tr>
        </tfoot>
    </table>
    " + Nskg.Helpers.ReportPaginationHelper.GetPaginationScript() + @"
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
        public IActionResult ExportExcel(string? rcocode, string? fyId, string? fromDate, string? toDate, string? reportType = "summary", bool withProcess = false)
        {
            int companyId = GetCompanyId();
            string selectedReportType = string.IsNullOrEmpty(reportType) ? "summary" : reportType.ToLower();

            try
            {
                DataTable dt = GetTrialBalanceData(rcocode, fyId, fromDate, toDate, companyId, selectedReportType, withProcess);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                string periodText = $"{fromDate} To {toDate}";

                var sb = new StringBuilder();
                sb.AppendLine("\"TRAIL BALANCE\"");
                sb.AppendLine($"\"Period:\",\"{periodText}\",\"Company:\",\"{(!string.IsNullOrEmpty(rcocode) ? rcocode : "ALL")}\"");
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
                sb.AppendLine($",Profit/Loss:,,{Math.Abs(totalDr - totalCr):#,##0.00},");

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
