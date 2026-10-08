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
    public class AccountLedgerReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public AccountLedgerReportController(ApplicationDbContext context, IConfiguration config, IWebHostEnvironment env)
        {
            _context = context;
            _config = config;
            _env = env;
        }

        private int GetCompanyId(int? companyId = null)
        {
            if (companyId.HasValue) return companyId.Value;
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
        public IActionResult Index(string accode, string fromDate, string toDate, int? companyId)
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

            var query = _context.GLChart3
                .Where(x => (selectedCompanyId == 0 || x.CompanyId == selectedCompanyId || x.CompanyId == 0 || x.CompanyId == null) && !string.IsNullOrEmpty(x.Name));

            var accounts = query
                .Select(x => new
                {
                    Code = (x.ACC ?? (x.AC1 + x.AC3)).Trim(),
                    Name = x.Name.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.Code))
                .ToList()
                .GroupBy(x => x.Code)
                .Select(g => new
                {
                    Code = g.Key,
                    Name = g.First().Name
                })
                .OrderBy(x => x.Code)
                .ToList();

            ViewBag.CompanyList = companies;
            ViewBag.SelectedCompanyId = selectedCompanyId;
            ViewBag.AccountList = accounts;
            ViewBag.Accode = accode;
            ViewBag.FromDate = string.IsNullOrEmpty(fromDate) ? DateTime.Now.ToString("yyyy-MM-dd") : fromDate;
            ViewBag.ToDate = string.IsNullOrEmpty(toDate) ? DateTime.Now.ToString("yyyy-MM-dd") : toDate;

            return View();
        }

        [HttpGet]
        public IActionResult GetAccountsByCompany(int companyId)
        {
            var query = _context.GLChart3
                .Where(x => (companyId == 0 || x.CompanyId == companyId || x.CompanyId == 0 || x.CompanyId == null) && !string.IsNullOrEmpty(x.Name));

            var accounts = query
                .Select(x => new
                {
                    code = (x.ACC ?? (x.AC1 + x.AC3)).Trim(),
                    name = x.Name.Trim()
                })
                .Where(x => !string.IsNullOrEmpty(x.code))
                .ToList()
                .GroupBy(x => x.code)
                .Select(g => new
                {
                    code = g.Key,
                    name = g.First().name
                })
                .OrderBy(x => x.code)
                .ToList();

            return Json(accounts);
        }

        private DataTable GetAccountLedger(string accode, DateTime fromDate, DateTime toDate, int companyId, int financialYearId)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                // 1. Run dbo.PROCESSDETAIL to populate ACCUMULATED & ACCOPEN
                try
                {
                    if (companyId > 0)
                    {
                        using (SqlCommand cmdProc = new SqlCommand("dbo.PROCESSDETAIL", con))
                        {
                            cmdProc.CommandType = CommandType.StoredProcedure;
                            cmdProc.CommandTimeout = 180;
                            cmdProc.Parameters.AddWithValue("@CompanyId", companyId);
                            cmdProc.Parameters.AddWithValue("@TDATE", toDate.Date);
                            cmdProc.Parameters.AddWithValue("@ACCODE", accode?.Trim() ?? "");
                            cmdProc.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        var activeCompanies = _context.Companies.Where(c => !c.IsDeleted).Select(c => c.Id).ToList();
                        if (activeCompanies.Count > 0)
                        {
                            using (SqlCommand cmdSetup = new SqlCommand(@"
                                IF OBJECT_ID('tempdb..#AllAcc') IS NOT NULL DROP TABLE #AllAcc;
                                SELECT TOP 0 * INTO #AllAcc FROM ACCUMULATED;", con))
                            {
                                cmdSetup.ExecuteNonQuery();
                            }

                            foreach (var cId in activeCompanies)
                            {
                                using (SqlCommand cmdProc = new SqlCommand("dbo.PROCESSDETAIL", con))
                                {
                                    cmdProc.CommandType = CommandType.StoredProcedure;
                                    cmdProc.CommandTimeout = 180;
                                    cmdProc.Parameters.AddWithValue("@CompanyId", cId);
                                    cmdProc.Parameters.AddWithValue("@TDATE", toDate.Date);
                                    cmdProc.Parameters.AddWithValue("@ACCODE", accode?.Trim() ?? "");
                                    cmdProc.ExecuteNonQuery();
                                }

                                using (SqlCommand cmdCopy = new SqlCommand("INSERT INTO #AllAcc SELECT * FROM ACCUMULATED;", con))
                                {
                                    cmdCopy.ExecuteNonQuery();
                                }
                            }

                            using (SqlCommand cmdMerge = new SqlCommand(@"
                                DELETE FROM ACCUMULATED;
                                INSERT INTO ACCUMULATED SELECT * FROM #AllAcc;
                                DROP TABLE #AllAcc;", con))
                            {
                                cmdMerge.ExecuteNonQuery();
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback if procedure encounters warning
                }

                // 2. Fetch Account Name & Company Name
                string accName = "";
                using (SqlCommand cmdAcc = new SqlCommand(
                    "SELECT TOP 1 Name FROM GLCHART3 WHERE (@CompanyId = 0 OR CompanyId = @CompanyId OR CompanyId = 0 OR CompanyId IS NULL) AND (RTRIM(AC1) + RTRIM(AC3) = RTRIM(@Accode) OR ACC = RTRIM(@Accode))", con))
                {
                    cmdAcc.Parameters.AddWithValue("@CompanyId", companyId);
                    cmdAcc.Parameters.AddWithValue("@Accode", accode?.Trim() ?? "");
                    var res = cmdAcc.ExecuteScalar();
                    if (res != null && res != DBNull.Value) accName = res.ToString();
                }

                string companyName = "West Wharf-New Shadab Karachi Goods Transports";
                string shortCompanyName = "W.W";
                string cocode = "";
                if (companyId > 0)
                {
                    using (SqlCommand cmdComp = new SqlCommand("SELECT TOP 1 Name, Cocode FROM Companies WHERE Id = @CompanyId", con))
                    {
                        cmdComp.Parameters.AddWithValue("@CompanyId", companyId);
                        using (var readerComp = cmdComp.ExecuteReader())
                        {
                            if (readerComp.Read())
                            {
                                if (readerComp["Name"] != DBNull.Value && !string.IsNullOrWhiteSpace(readerComp["Name"].ToString()))
                                    companyName = readerComp["Name"].ToString()!;
                                if (readerComp["Cocode"] != DBNull.Value)
                                    cocode = readerComp["Cocode"].ToString() ?? "";
                            }
                        }
                    }

                    if (cocode == "01" || companyId == 1006) shortCompanyName = "W.W";
                    else if (cocode == "02" || companyId == 1007) shortCompanyName = "M.P";
                    else if (cocode == "03" || companyId == 1008) shortCompanyName = "N.K";
                    else if (cocode == "04" || companyId == 1009) shortCompanyName = "R.W";
                }
                else
                {
                    companyName = "New Shadab Karachi Goods Transport Company (All Branches - Linked)";
                    shortCompanyName = "LINKED";
                }

                // 3. Query Opening Balance and Transactions with Running Balance and robust Vehicle/Station resolution
                string query = @"
                    DECLARE @AnnualOpeningBal DECIMAL(18,2) = 0;

                    IF OBJECT_ID('OpeningBalances', 'U') IS NOT NULL
                    BEGIN
                        SELECT @AnnualOpeningBal = ISNULL(SUM(Debit - Credit), 0)
                        FROM OpeningBalances
                        WHERE Accode = @Accode
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId)
                          AND FinancialYearId = @FinancialYearId;
                    END

                    DECLARE @OpeningBal DECIMAL(18,2) = 0;

                    SELECT @OpeningBal = @AnnualOpeningBal + ISNULL(SUM(ISNULL(DRAMT, 0) - ISNULL(CRAMT, 0)), 0)
                    FROM ACCUMULATED
                    WHERE VODATE < @FromDate;

                    ;WITH RawData AS
                    (
                        -- Opening Balance Row
                        SELECT 
                            0 AS SortOrder,
                            CAST(NULL AS DATE) AS DocDate,
                            CAST('' AS VARCHAR(50)) AS DocNo,
                            CAST('' AS VARCHAR(10)) AS Votype,
                            CAST(NULL AS VARCHAR(50)) AS BillTiNo,
                            CAST(NULL AS VARCHAR(50)) AS BilNo,
                            CAST('OPENING BALANCE' AS VARCHAR(100)) AS VehicleNo,
                            CAST('' AS VARCHAR(100)) AS Station,
                            CAST('' AS VARCHAR(250)) AS IName,
                            CAST(NULL AS DECIMAL(18,2)) AS Qty,
                            CAST('' AS VARCHAR(100)) AS Initials,
                            CASE WHEN @OpeningBal > 0 THEN @OpeningBal ELSE CAST(0 AS DECIMAL(18,2)) END AS Debit,
                            CASE WHEN @OpeningBal < 0 THEN ABS(@OpeningBal) ELSE CAST(0 AS DECIMAL(18,2)) END AS Credit,
                            @OpeningBal AS Balance,
                            CAST(0 AS BIGINT) AS RowNum

                        UNION ALL

                        -- Transactions between FromDate and ToDate
                        SELECT 
                            1 AS SortOrder,
                            CAST(a.VODATE AS DATE) AS DocDate,
                            ISNULL(a.VONO, '') AS DocNo,
                            COALESCE(
                                NULLIF(RTRIM(LTRIM(a.VOTYPE)), ''),
                                (SELECT TOP 1 NULLIF(RTRIM(LTRIM(vd.Votype)), '') FROM VoDet vd WHERE vd.Vono = a.VONO),
                                (SELECT TOP 1 NULLIF(RTRIM(LTRIM(vh.Votype)), '') FROM VoHead vh WHERE vh.Vono = a.VONO),
                                CASE 
                                    WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN 'BL'
                                    WHEN a.VONO LIKE '%CL%' THEN 'CL'
                                    ELSE ''
                                END
                            ) AS Votype,
                            COALESCE(
                                CASE WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN CAST(CAST(a.BILLTINO AS BIGINT) AS VARCHAR(50)) ELSE NULL END,
                                (SELECT TOP 1 CAST(CAST(ish.BillTiNo AS BIGINT) AS VARCHAR(50)) FROM IssHead ish WHERE ish.DocNo = a.VONO AND ish.BillTiNo IS NOT NULL AND ish.BillTiNo <> 0 AND ISNULL(ish.IsDeleted, 0) = 0),
                                (SELECT TOP 1 CAST(CAST(vd.Billtino AS BIGINT) AS VARCHAR(50)) FROM VoDet vd WHERE vd.Vono = a.VONO AND vd.Billtino IS NOT NULL AND TRY_CAST(vd.Billtino AS BIGINT) > 0 AND ISNULL(vd.IsDeleted, 0) = 0),
                                NULL
                            ) AS BillTiNo,
                            COALESCE(
                                CASE WHEN a.BILNO IS NOT NULL AND a.BILNO <> 0 THEN CAST(CAST(a.BILNO AS BIGINT) AS VARCHAR(50)) ELSE NULL END,
                                CASE WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN (SELECT TOP 1 CAST(CAST(ish.BilNo AS BIGINT) AS VARCHAR(50)) FROM IssHead ish WHERE ish.BillTiNo = a.BILLTINO AND ish.BilNo IS NOT NULL AND ish.BilNo <> 0 AND ISNULL(ish.IsDeleted, 0) = 0) ELSE NULL END,
                                (SELECT TOP 1 CAST(CAST(ish.BilNo AS BIGINT) AS VARCHAR(50)) FROM IssHead ish WHERE ish.DocNo = a.VONO AND ish.BilNo IS NOT NULL AND ish.BilNo <> 0 AND ISNULL(ish.IsDeleted, 0) = 0),
                                (SELECT TOP 1 CAST(CAST(vd.Bilno AS BIGINT) AS VARCHAR(50)) FROM VoDet vd WHERE vd.Vono = a.VONO AND vd.Bilno IS NOT NULL AND TRY_CAST(vd.Bilno AS BIGINT) > 0 AND ISNULL(vd.IsDeleted, 0) = 0),
                                NULL
                            ) AS BilNo,
                            COALESCE(
                                NULLIF(RTRIM(LTRIM(a.VEHICLENO)), ''),
                                -- From VoDet.Vehicleno
                                (SELECT TOP 1 NULLIF(RTRIM(LTRIM(vd.Vehicleno)), '') FROM VoDet vd WHERE vd.Vono = a.VONO AND NULLIF(RTRIM(LTRIM(vd.Vehicleno)), '') IS NOT NULL),
                                -- From ChallanDet matching BillTiNo and BilNo
                                CASE WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN (
                                    SELECT TOP 1 NULLIF(RTRIM(LTRIM(cd.VehicleNo)), '') 
                                    FROM ChallanDet cd 
                                    WHERE cd.BillTiNo = a.BILLTINO 
                                      AND (a.BILNO IS NULL OR a.BILNO = 0 OR cd.BilNo = a.BILNO)
                                      AND NULLIF(RTRIM(LTRIM(cd.VehicleNo)), '') IS NOT NULL
                                ) ELSE NULL END,
                                -- From ChallanHead matching ChallanDet BillTiNo
                                CASE WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN (
                                    SELECT TOP 1 NULLIF(RTRIM(LTRIM(ch.VehicleNo)), '') 
                                    FROM ChallanDet cd 
                                    INNER JOIN ChallanHead ch ON cd.ChallanHeadId = ch.Id 
                                    WHERE cd.BillTiNo = a.BILLTINO 
                                      AND NULLIF(RTRIM(LTRIM(ch.VehicleNo)), '') IS NOT NULL
                                      AND ISNULL(ch.IsDeleted, 0) = 0
                                ) ELSE NULL END,
                                -- From CommHead matching BillTiNo
                                CASE WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN (
                                    SELECT TOP 1 NULLIF(RTRIM(LTRIM(cm.VehicleNo)), '') 
                                    FROM CommDetail cmd 
                                    INNER JOIN CommHead cm ON cmd.CommHeadId = cm.Id 
                                    WHERE cmd.BillTiNo = a.BILLTINO 
                                      AND NULLIF(RTRIM(LTRIM(cm.VehicleNo)), '') IS NOT NULL
                                      AND ISNULL(cm.IsDeleted, 0) = 0
                                ) ELSE NULL END,
                                -- From IssHead matching BillTiNo
                                CASE WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN (
                                    SELECT TOP 1 NULLIF(RTRIM(LTRIM(iss.VehicleNo)), '') 
                                    FROM IssHead iss 
                                    WHERE iss.BillTiNo = a.BILLTINO 
                                      AND NULLIF(RTRIM(LTRIM(iss.VehicleNo)), '') IS NOT NULL
                                      AND ISNULL(iss.IsDeleted, 0) = 0
                                ) ELSE NULL END,
                                -- If Challan / Narration has vehicle
                                CASE WHEN a.VOTYPE = 'CL' AND NULLIF(RTRIM(LTRIM(a.NARRATION)), '') IS NOT NULL AND a.NARRATION NOT LIKE '%-%-%' THEN RTRIM(LTRIM(a.NARRATION)) ELSE NULL END,
                                CASE WHEN a.VOTYPE IN ('CR', 'CP', 'BR', 'BP', 'JV') AND NULLIF(RTRIM(LTRIM(a.NARRATION)), '') IS NOT NULL AND (a.NARRATION LIKE '%[0-9]%' OR a.NARRATION LIKE '%-%') THEN RTRIM(LTRIM(a.NARRATION)) ELSE NULL END,
                                ''
                            ) AS VehicleNo,
                            COALESCE(
                                NULLIF(RTRIM(LTRIM(a.STATION)), ''),
                                ''
                            ) AS Station,
                            ISNULL(a.INAME, ISNULL(a.NARRATION, '')) AS IName,
                            COALESCE(
                                NULLIF(a.QTY, 0),
                                CASE WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN (SELECT TOP 1 ish.Qty FROM IssHead ish WHERE ish.BillTiNo = a.BILLTINO AND ish.Qty IS NOT NULL AND ish.Qty <> 0 AND ISNULL(ish.IsDeleted, 0) = 0) ELSE NULL END,
                                CASE WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN (SELECT SUM(id.Qty) FROM IssDetail id INNER JOIN IssHead ish ON id.IssHeadId = ish.Id WHERE ish.BillTiNo = a.BILLTINO AND ISNULL(id.IsDeleted, 0) = 0) ELSE NULL END,
                                (SELECT TOP 1 ish.Qty FROM IssHead ish WHERE ish.DocNo = a.VONO AND ish.Qty IS NOT NULL AND ish.Qty <> 0 AND ISNULL(ish.IsDeleted, 0) = 0),
                                (SELECT SUM(id.Qty) FROM IssDetail id INNER JOIN IssHead ish ON id.IssHeadId = ish.Id WHERE ish.DocNo = a.VONO AND ISNULL(id.IsDeleted, 0) = 0),
                                (SELECT TOP 1 vd.Qty FROM VoDet vd WHERE vd.Vono = a.VONO AND vd.Qty IS NOT NULL AND vd.Qty <> 0 AND ISNULL(vd.IsDeleted, 0) = 0),
                                NULL
                            ) AS Qty,
                            CASE 
                                WHEN RTRIM(LTRIM(ISNULL(a.COCODE, ''))) IN ('01', '1006', '1') THEN 'W.W'
                                WHEN RTRIM(LTRIM(ISNULL(a.COCODE, ''))) IN ('02', '1007', '2') THEN 'M.P'
                                WHEN RTRIM(LTRIM(ISNULL(a.COCODE, ''))) IN ('03', '1008', '3') THEN 'N.K'
                                WHEN RTRIM(LTRIM(ISNULL(a.COCODE, ''))) IN ('04', '1009', '4') THEN 'R.W'
                                WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN 
                                    CASE (SELECT TOP 1 ish.CompanyId FROM IssHead ish WHERE ish.BillTiNo = a.BILLTINO AND ISNULL(ish.IsDeleted, 0) = 0)
                                        WHEN 1006 THEN 'W.W'
                                        WHEN 1007 THEN 'M.P'
                                        WHEN 1008 THEN 'N.K'
                                        WHEN 1009 THEN 'R.W'
                                        ELSE (
                                            CASE (SELECT TOP 1 ish.Cocode FROM IssHead ish WHERE ish.BillTiNo = a.BILLTINO AND ISNULL(ish.IsDeleted, 0) = 0)
                                                WHEN '01' THEN 'W.W'
                                                WHEN '02' THEN 'M.P'
                                                WHEN '03' THEN 'N.K'
                                                WHEN '04' THEN 'R.W'
                                                ELSE 'W.W'
                                            END
                                        )
                                    END
                                WHEN a.VONO IS NOT NULL AND a.VONO <> '' THEN
                                    CASE (SELECT TOP 1 vh.CompanyId FROM VoHead vh WHERE vh.Vono = a.VONO AND ISNULL(vh.IsDeleted, 0) = 0)
                                        WHEN 1006 THEN 'W.W'
                                        WHEN 1007 THEN 'M.P'
                                        WHEN 1008 THEN 'N.K'
                                        WHEN 1009 THEN 'R.W'
                                        ELSE (
                                            CASE (SELECT TOP 1 vd.Cocode FROM VoDet vd WHERE vd.Vono = a.VONO AND ISNULL(vd.IsDeleted, 0) = 0)
                                                WHEN '01' THEN 'W.W'
                                                WHEN '02' THEN 'M.P'
                                                WHEN '03' THEN 'N.K'
                                                WHEN '04' THEN 'R.W'
                                                ELSE 'W.W'
                                            END
                                        )
                                    END
                                WHEN @CompanyId = 1006 OR @CompanyId = 1 THEN 'W.W'
                                WHEN @CompanyId = 1007 OR @CompanyId = 2 THEN 'M.P'
                                WHEN @CompanyId = 1008 OR @CompanyId = 3 THEN 'N.K'
                                WHEN @CompanyId = 1009 OR @CompanyId = 4 THEN 'R.W'
                                ELSE 'W.W'
                            END AS Initials,
                            ISNULL(a.DRAMT, 0) AS Debit,
                            ISNULL(a.CRAMT, 0) AS Credit,
                            @OpeningBal + SUM(ISNULL(a.DRAMT, 0) - ISNULL(a.CRAMT, 0)) OVER (
                                ORDER BY a.VODATE, a.VONO, (SELECT NULL)
                                ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                            ) AS Balance,
                            ROW_NUMBER() OVER (ORDER BY a.VODATE, a.VONO) AS RowNum
                        FROM ACCUMULATED a
                        WHERE a.VODATE >= @FromDate AND a.VODATE <= @ToDate
                    )
                    SELECT 
                        SortOrder,
                        DocDate,
                        DocNo,
                        Votype,
                        BillTiNo,
                        BilNo,
                        VehicleNo,
                        Station,
                        INAME,
                        Qty,
                        Initials,
                        Debit,
                        Credit,
                        Balance,
                        @Accode AS Accode,
                        @AccName AS AccName,
                        @CompanyName AS CompanyName,
                        @ShortCompanyName AS ShortCompanyName,
                        @FromDate AS FromDate,
                        @ToDate AS ToDate
                    FROM RawData
                    ORDER BY SortOrder, DocDate, DocNo, RowNum;";

                using (SqlCommand cmdData = new SqlCommand(query, con))
                {
                    cmdData.CommandTimeout = 180;
                    cmdData.Parameters.AddWithValue("@FromDate", fromDate.Date);
                    cmdData.Parameters.AddWithValue("@ToDate", toDate.Date);
                    cmdData.Parameters.AddWithValue("@Accode", accode?.Trim() ?? "");
                    cmdData.Parameters.AddWithValue("@AccName", accName);
                    cmdData.Parameters.AddWithValue("@CompanyName", companyName);
                    cmdData.Parameters.AddWithValue("@ShortCompanyName", shortCompanyName);
                    cmdData.Parameters.AddWithValue("@CompanyId", companyId);
                    cmdData.Parameters.AddWithValue("@FinancialYearId", financialYearId);

                    using (SqlDataReader reader = cmdData.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult GeneratePDF(string accode, DateTime fromDate, DateTime toDate, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetAccountLedger(accode, fromDate, toDate, selectedCompanyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found for selected date range and account.");
                }

                string reportPath = Path.Combine(_env.WebRootPath, "Reports", "AccountLedgerrpt.rdlc");

                if (!System.IO.File.Exists(reportPath))
                {
                    return Content($"Report file not found: {reportPath}");
                }

                using (LocalReport report = new LocalReport())
                {
                    report.ReportPath = reportPath;
                    report.DataSources.Clear();

                    dt.TableName = "DSAccountLedger";
                    report.DataSources.Add(new ReportDataSource("DSAccountLedger", dt));

                    byte[] pdfBytes = report.Render("PDF");

                    if (pdfBytes == null || pdfBytes.Length < 100)
                    {
                        return Content("PDF generation failed or corrupted output.");
                    }

                    return File(pdfBytes, "application/pdf", $"AccountLedger_{accode}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf", enableRangeProcessing: true);
                }
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult OnScreenReport(string accode, DateTime fromDate, DateTime toDate, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetAccountLedger(accode, fromDate, toDate, selectedCompanyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No data found for selected date range and account.</strong></div>", "text/html");
                }

                string compName = (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company";
                string accName = (dt.Rows.Count > 0 ? dt.Rows[0]["AccName"]?.ToString() : null) ?? "";
                string shortCompName = (dt.Rows.Count > 0 && dt.Columns.Contains("ShortCompanyName") ? dt.Rows[0]["ShortCompanyName"]?.ToString() : null) ?? "W.W";

                decimal totalDebit = 0, totalCredit = 0;
                var sb = new StringBuilder();
                sb.Append(@"<!DOCTYPE html><html><head><meta charset='utf-8'>
                <style>
                    body{font-family:Arial,sans-serif;font-size:12px;margin:12px;color:#333;}
                    .header-box{margin-bottom:12px;}
                    .header-top{display:flex;justify-content:space-between;align-items:center;margin-bottom:4px;}
                    .comp-initial{font-size:14px;font-weight:bold;border:2px solid #0d6efd;color:#0d6efd;padding:2px 10px;border-radius:4px;background:#f0f7ff;}
                    .header-box h2{margin:0;color:#0d6efd;font-size:18px;text-align:center;flex-grow:1;}
                    .print-meta{font-size:11px;color:#6c757d;text-align:right;}
                    .header-box h3{margin:4px 0;font-size:14px;color:#495057;text-align:center;}
                    .header-box p{margin:0;font-size:11px;color:#495057;text-align:center;}
                    table{width:100%;border-collapse:collapse;margin-top:8px;}
                    th{background:#0d6efd;color:#fff;padding:7px 6px;text-align:left;font-size:11px;border:1px solid #0b5ed7;}
                    td{padding:5px 6px;border:1px solid #dee2e6;font-size:11px;}
                    tr:nth-child(even){background:#f8f9fa;}
                    tr:hover{background:#e9ecef;}
                    .num{text-align:right;}
                    .bold{font-weight:bold;}
                    .op-row{background:#e7f1ff;font-weight:bold;}
                    tfoot tr{background:#d0e2ff;font-weight:bold;}
                    " + Nskg.Helpers.ReportPaginationHelper.GetPaginationStyles() + @"
                </style></head><body>");

                sb.Append(Nskg.Helpers.ReportPaginationHelper.GetPaginationToolbarHtml("Account Ledger"));
                sb.Append($"<div class='header-box'>");
                sb.Append($"<div class='header-top'>");
                sb.Append($"<div class='comp-initial'>{shortCompName}</div>");
                sb.Append($"<h2>{compName}</h2>");
                sb.Append($"<div class='print-meta'>Print Date: {DateTime.Now:dd-MMM-yyyy HH:mm:ss}</div>");
                sb.Append($"</div>");
                sb.Append($"<div style='display:flex; justify-content:space-between; align-items:center; margin:8px 0 4px 0; padding-bottom:4px; border-bottom:1.5px solid #000;'>");
                sb.Append($"<div style='font-size:15px; font-weight:bold;'><span style='letter-spacing:1px;'>{accode}</span> &nbsp;&nbsp;&nbsp;&nbsp; <span style='color:#0d6efd;'>{accName}</span></div>");
                sb.Append($"<div style='font-size:11px; color:#495057;'>Period: <b>{fromDate:dd-MMM-yyyy}</b> to <b>{toDate:dd-MMM-yyyy}</b></div>");
                sb.Append($"</div>");
                sb.Append($"</div>");

                sb.Append("<table><thead><tr>");
                sb.Append("<th>Date</th><th>Doc #</th><th>Typ</th><th>Bilty #</th><th>Bill #</th><th>Vehicle No</th><th>Party / Item</th><th class='num'>Qty</th><th>Initials</th><th class='num'>Debit</th><th class='num'>Credit</th><th class='num'>Balance</th>");
                sb.Append("</tr></thead><tbody>");

                foreach (DataRow row in dt.Rows)
                {
                    string docDate = row["DocDate"] != DBNull.Value && row["DocDate"] != null ? Convert.ToDateTime(row["DocDate"]).ToString("dd-MMM-yy") : "";
                    string docNo = row["DocNo"]?.ToString() ?? "";
                    string votype = row["Votype"]?.ToString() ?? "";
                    string biltiNo = row["BillTiNo"]?.ToString() ?? "";
                    string bilNo = row["BilNo"]?.ToString() ?? "";
                    string vNo = row["VehicleNo"]?.ToString() ?? "";
                    string iname = row["IName"]?.ToString() ?? "";
                    string qty = row["Qty"] != DBNull.Value && row["Qty"] != null ? (row["Qty"]?.ToString() ?? "") : "";
                    string initials = row["Initials"]?.ToString() ?? "";

                    decimal debit = row["Debit"] != DBNull.Value && row["Debit"] != null ? Convert.ToDecimal(row["Debit"]) : 0;
                    decimal credit = row["Credit"] != DBNull.Value && row["Credit"] != null ? Convert.ToDecimal(row["Credit"]) : 0;
                    decimal balance = row["Balance"] != DBNull.Value && row["Balance"] != null ? Convert.ToDecimal(row["Balance"]) : 0;

                    int sortOrder = row["SortOrder"] != DBNull.Value ? Convert.ToInt32(row["SortOrder"]) : 1;
                    if (sortOrder > 0)
                    {
                        totalDebit += debit;
                        totalCredit += credit;
                    }

                    string rowClass = sortOrder == 0 ? "class='op-row'" : "";
                    string debitStr = debit != 0 ? debit.ToString("#,##0.00") : "";
                    string creditStr = credit != 0 ? credit.ToString("#,##0.00") : "";

                    sb.Append($"<tr {rowClass}>");
                    sb.Append($"<td>{docDate}</td><td>{docNo}</td><td>{votype}</td><td>{biltiNo}</td><td>{bilNo}</td><td>{vNo}</td><td>{iname}</td><td class='num'>{qty}</td><td>{initials}</td><td class='num'>{debitStr}</td><td class='num'>{creditStr}</td><td class='num bold'>{balance:#,##0.00}</td>");
                    sb.Append($"</tr>");
                }

                sb.Append("</tbody><tfoot><tr>");
                sb.Append($"<td colspan='9' style='text-align:right;' class='bold'>TOTALS:</td><td class='num bold'>{totalDebit:#,##0.00}</td><td class='num bold'>{totalCredit:#,##0.00}</td><td></td>");
                sb.Append("</tr></tfoot></table>");
                sb.Append(Nskg.Helpers.ReportPaginationHelper.GetPaginationScript());
                sb.Append("</body></html>");

                return Content(sb.ToString(), "text/html");
            }
            catch (Exception ex)
            {
                return Content($"<div style='color:red;padding:20px;'><strong>ERROR:</strong> {ex.Message}</div>", "text/html");
            }
        }

        [HttpGet]
        public IActionResult ExportExcel(string accode, DateTime fromDate, DateTime toDate, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetAccountLedger(accode, fromDate, toDate, selectedCompanyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                var sb = new StringBuilder();

                // Company Header
                string compName = (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company";
                string accName = (dt.Rows.Count > 0 ? dt.Rows[0]["AccName"]?.ToString() : null) ?? "";
                sb.AppendLine($"\"{compName}\"");
                sb.AppendLine("\"GENERAL LEDGER\"");
                sb.AppendLine($"\"Account:\",\"{accode} - {accName}\",\"From:\",\"{fromDate:dd-MM-yyyy}\",\"To:\",\"{toDate:dd-MM-yyyy}\"");
                sb.AppendLine();

                // Table Header
                sb.AppendLine("Doc.Date,Doc. #,Typ,BilltiNo,Bill. No,Vehicle No,Party name / Item,Qty,Initials,DEBIT,CREDIT,BALANCE");

                foreach (DataRow row in dt.Rows)
                {
                    string docDate = row["DocDate"] != DBNull.Value && row["DocDate"] != null ? Convert.ToDateTime(row["DocDate"]).ToString("dd-MM-yyyy") : "";
                    string docNo = EscapeCsv(row["DocNo"]?.ToString() ?? "");
                    string votype = EscapeCsv(row["Votype"]?.ToString() ?? "");
                    string billtiNo = EscapeCsv(row["BillTiNo"]?.ToString() ?? "");
                    string bilNo = EscapeCsv(row["BilNo"]?.ToString() ?? "");
                    string vehicleNo = EscapeCsv(row["VehicleNo"]?.ToString() ?? "");
                    string iname = EscapeCsv(row["IName"]?.ToString() ?? "");
                    string qty = row["Qty"] != DBNull.Value && row["Qty"] != null ? (row["Qty"]?.ToString() ?? "") : "";
                    string initials = EscapeCsv(row["Initials"]?.ToString() ?? "");
                    string debit = row["Debit"] != DBNull.Value && Convert.ToDecimal(row["Debit"]) != 0 ? Convert.ToDecimal(row["Debit"]).ToString("F2") : "";
                    string credit = row["Credit"] != DBNull.Value && Convert.ToDecimal(row["Credit"]) != 0 ? Convert.ToDecimal(row["Credit"]).ToString("F2") : "";
                    string balance = row["Balance"] != DBNull.Value ? Convert.ToDecimal(row["Balance"]).ToString("F2") : "";

                    sb.AppendLine($"{docDate},{docNo},{votype},{billtiNo},{bilNo},{vehicleNo},{iname},{qty},{initials},{debit},{credit},{balance}");
                }

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"AccountLedger_{accode}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv");
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
