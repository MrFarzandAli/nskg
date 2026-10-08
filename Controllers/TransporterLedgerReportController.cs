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
    public class TransporterLedgerReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public TransporterLedgerReportController(ApplicationDbContext context, IConfiguration config, IWebHostEnvironment env)
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
            return 0;
        }

        private int GetFinancialYearId()
        {
            var fyId = User.FindFirst("FinancialYearId")?.Value;
            if (int.TryParse(fyId, out int id) && id > 0) return id;
            return 4;
        }

        [HttpGet]
        public IActionResult GetAccountsByCompany(int companyId)
        {
            var accountsQuery = _context.GLChart3
                .Where(x => (companyId == 0 || x.CompanyId == companyId || x.CompanyId == 0 || x.CompanyId == null) && !string.IsNullOrEmpty(x.Name));

            var accounts = accountsQuery
                .Where(x => x.AC1 == "052" || x.AC1 == "060" || x.Name.Contains("BROKER") || x.Name.Contains("TRANSPORTER") || x.Name.Contains("TRANS"))
                .Select(x => new
                {
                    Code = (x.ACC ?? (x.AC1 + x.AC3)).Trim(),
                    Name = x.Name.Trim()
                })
                .ToList()
                .GroupBy(x => x.Code)
                .Select(g => new
                {
                    code = g.Key,
                    Code = g.Key,
                    name = g.First().Name,
                    Name = g.First().Name
                })
                .OrderBy(x => x.name)
                .ToList();

            if (accounts.Count == 0)
            {
                accounts = accountsQuery
                    .Select(x => new
                    {
                        Code = (x.ACC ?? (x.AC1 + x.AC3)).Trim(),
                        Name = x.Name.Trim()
                    })
                    .ToList()
                    .GroupBy(x => x.Code)
                    .Select(g => new
                    {
                        code = g.Key,
                        Code = g.Key,
                        name = g.First().Name,
                        Name = g.First().Name
                    })
                    .OrderBy(x => x.name)
                    .ToList();
            }

            return Json(accounts);
        }

        [HttpGet]
        public IActionResult GetVehiclesByCompany(int companyId)
        {
            var vehicles = _context.VoDet
                .Where(v => v.Vehicleno != null && v.Vehicleno.Trim() != "" && (companyId == 0 || v.VoHead == null || v.VoHead.CompanyId == companyId || v.VoHead.CompanyId == 0))
                .Select(v => v.Vehicleno!.Trim())
                .Union(_context.ChallanHead
                    .Where(c => c.VehicleNo != null && c.VehicleNo.Trim() != "" && (companyId == 0 || c.CompanyId == companyId || c.CompanyId == 0))
                    .Select(c => c.VehicleNo!.Trim()))
                .Union(_context.CommHead
                    .Where(c => c.VehicleNo != null && c.VehicleNo.Trim() != "" && (companyId == 0 || c.CompanyId == companyId || c.CompanyId == 0))
                    .Select(c => c.VehicleNo!.Trim()))
                .Where(v => v != "")
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            return Json(vehicles);
        }

        [HttpGet]
        public IActionResult Index(string? accode, string? fromDate, string? toDate, string? vehicleNo, int? companyId)
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

            var accountsQuery = _context.GLChart3
                .Where(x => (selectedCompanyId == 0 || x.CompanyId == selectedCompanyId || x.CompanyId == 0 || x.CompanyId == null) && !string.IsNullOrEmpty(x.Name));

            var accounts = accountsQuery
                .Where(x => x.AC1 == "052" || x.AC1 == "060" || x.Name.Contains("BROKER") || x.Name.Contains("TRANSPORTER") || x.Name.Contains("TRANS"))
                .Select(x => new
                {
                    Code = (x.ACC ?? (x.AC1 + x.AC3)).Trim(),
                    Name = x.Name.Trim()
                })
                .ToList()
                .GroupBy(x => x.Code)
                .Select(g => new
                {
                    Code = g.Key,
                    Name = g.First().Name
                })
                .OrderBy(x => x.Name)
                .ToList();

            if (accounts.Count == 0)
            {
                accounts = accountsQuery
                    .Select(x => new
                    {
                        Code = (x.ACC ?? (x.AC1 + x.AC3)).Trim(),
                        Name = x.Name.Trim()
                    })
                    .ToList()
                    .GroupBy(x => x.Code)
                    .Select(g => new
                    {
                        Code = g.Key,
                        Name = g.First().Name
                    })
                    .OrderBy(x => x.Name)
                    .ToList();
            }

            ViewBag.CompanyList = companies;
            ViewBag.SelectedCompanyId = selectedCompanyId;
            ViewBag.AccountList = accounts;
            ViewBag.Accode = accode;
            ViewBag.VehicleNo = vehicleNo;
            ViewBag.FromDate = string.IsNullOrEmpty(fromDate) ? "2010-01-01" : fromDate;
            ViewBag.ToDate = string.IsNullOrEmpty(toDate) ? DateTime.Now.ToString("yyyy-MM-dd") : toDate;

            return View();
        }

        private DataTable GetTransporterLedger(string accode, DateTime fromDate, DateTime toDate, string vehicleNo, int companyId, int financialYearId)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                // 1. Run dbo.sp_ProcessTransporterLedger to populate ACCUMULATED & ACCOPEN
                try
                {
                    using (SqlCommand cmdProc = new SqlCommand("dbo.sp_ProcessTransporterLedger", con))
                    {
                        cmdProc.CommandType = CommandType.StoredProcedure;
                        cmdProc.CommandTimeout = 180;
                        cmdProc.Parameters.AddWithValue("@CompanyId", companyId);
                        cmdProc.Parameters.AddWithValue("@TDATE", toDate.Date);
                        cmdProc.Parameters.AddWithValue("@ACCODE", accode?.Trim() ?? "");
                        cmdProc.Parameters.AddWithValue("@VehicleNo", vehicleNo?.Trim() ?? "");
                        cmdProc.ExecuteNonQuery();
                    }
                }
                catch
                {
                    try
                    {
                        using (SqlCommand cmdProc2 = new SqlCommand("dbo.PROCESSDETAIL_Transporter", con))
                        {
                            cmdProc2.CommandType = CommandType.StoredProcedure;
                            cmdProc2.CommandTimeout = 180;
                            cmdProc2.Parameters.AddWithValue("@CompanyId", companyId);
                            cmdProc2.Parameters.AddWithValue("@TDATE", toDate.Date);
                            cmdProc2.Parameters.AddWithValue("@ACCODE", accode?.Trim() ?? "");
                            cmdProc2.Parameters.AddWithValue("@VehicleNo", vehicleNo?.Trim() ?? "");
                            cmdProc2.ExecuteNonQuery();
                        }
                    }
                    catch { }
                }

                // 2. Fetch Account Name & Company Name
                string accName = "ALL TRANSPORTERS";
                if (!string.IsNullOrWhiteSpace(accode))
                {
                    using (SqlCommand cmdAcc = new SqlCommand(
                        "SELECT TOP 1 Name FROM GLCHART3 WHERE (@CompanyId = 0 OR CompanyId = @CompanyId OR CompanyId IS NULL) AND (RTRIM(AC1) + RTRIM(AC3) = RTRIM(@Accode) OR ACC = RTRIM(@Accode))", con))
                    {
                        cmdAcc.Parameters.AddWithValue("@CompanyId", companyId);
                        cmdAcc.Parameters.AddWithValue("@Accode", accode.Trim());
                        var res = cmdAcc.ExecuteScalar();
                        if (res != null && res != DBNull.Value && !string.IsNullOrWhiteSpace(res.ToString()))
                            accName = res.ToString()!.Trim();
                    }

                    if (accName == "ALL TRANSPORTERS")
                    {
                        using (SqlCommand cmdAccFallback = new SqlCommand(
                            "SELECT TOP 1 Name FROM GLCHART3 WHERE (RTRIM(AC1) + RTRIM(AC3) = RTRIM(@Accode) OR ACC = RTRIM(@Accode))", con))
                        {
                            cmdAccFallback.Parameters.AddWithValue("@Accode", accode.Trim());
                            var res = cmdAccFallback.ExecuteScalar();
                            if (res != null && res != DBNull.Value && !string.IsNullOrWhiteSpace(res.ToString()))
                                accName = res.ToString()!.Trim();
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(vehicleNo))
                {
                    if (string.IsNullOrWhiteSpace(accode))
                        accName = $"TRANSPORTER LEDGER ({vehicleNo.Trim()})";
                    else
                        accName += $" (VEHICLE: {vehicleNo.Trim()})";
                }

                string companyName = companyId == 0 
                    ? "New Shadab Karachi Goods Transport Company (All Branches - Linked)" 
                    : "West Wharf-New Shadab Karachi Goods Transports";
                if (companyId > 0)
                {
                    using (SqlCommand cmdComp = new SqlCommand("SELECT TOP 1 Name FROM Companies WHERE Id = @CompanyId", con))
                    {
                        cmdComp.Parameters.AddWithValue("@CompanyId", companyId);
                        var res = cmdComp.ExecuteScalar();
                        if (res != null && res != DBNull.Value && !string.IsNullOrWhiteSpace(res.ToString()))
                            companyName = res.ToString();
                    }
                }

                // 3. Query Opening Balance and Transactions with Running Balance
                string query = @"
                    DECLARE @AnnualOpeningBal DECIMAL(18,2) = 0;

                    IF OBJECT_ID('OpeningBalances', 'U') IS NOT NULL
                    BEGIN
                        SELECT @AnnualOpeningBal = ISNULL(SUM(Debit - Credit), 0)
                        FROM OpeningBalances
                        WHERE (@Accode = '' OR Accode = @Accode)
                          AND (@CompanyId = 0 OR CompanyId = @CompanyId)
                          AND FinancialYearId = @FinancialYearId;
                    END

                    DECLARE @OpeningBal DECIMAL(18,2) = 0;

                    SELECT @OpeningBal = @AnnualOpeningBal + ISNULL(SUM(ISNULL(DRAMT, 0) - ISNULL(CRAMT, 0)), 0)
                    FROM ACCUMULATED a
                    WHERE a.VODATE < @FromDate
                      AND (@Accode = '' OR RTRIM(LTRIM(ISNULL(a.ACC, ''))) = @Accode OR (RTRIM(LTRIM(ISNULL(a.AC1, ''))) + RTRIM(LTRIM(ISNULL(a.AC3, '')))) = @Accode)
                      AND (
                          @VehicleNo = '' 
                          OR RTRIM(LTRIM(ISNULL(a.VEHICLENO, ''))) = RTRIM(LTRIM(@VehicleNo))
                          OR a.VEHICLENO LIKE '%' + @VehicleNo + '%'
                          OR (a.VOTYPE = 'CL' AND a.NARRATION LIKE '%' + @VehicleNo + '%')
                          OR EXISTS (SELECT 1 FROM ISSHEAD ish WHERE (ish.BillTiNo = a.BILLTINO OR ish.BilNo = a.BILNO) AND ish.VehicleNo LIKE '%' + @VehicleNo + '%')
                          OR EXISTS (SELECT 1 FROM ChallanDet cd INNER JOIN ChallanHead ch ON cd.ChallanHeadId = ch.Id WHERE (cd.BillTiNo = a.BILLTINO OR cd.BilNo = a.BILNO) AND ch.VehicleNo LIKE '%' + @VehicleNo + '%')
                          OR EXISTS (SELECT 1 FROM CommDetail cmd INNER JOIN CommHead cm ON cmd.CommHeadId = cm.Id WHERE cmd.BillTiNo = a.BILLTINO AND cm.VehicleNo LIKE '%' + @VehicleNo + '%')
                      );

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
                            CASE WHEN @OpeningBal > 0 THEN @OpeningBal ELSE CAST(0 AS DECIMAL(18,2)) END AS Debit,
                            CASE WHEN @OpeningBal < 0 THEN ABS(@OpeningBal) ELSE CAST(0 AS DECIMAL(18,2)) END AS Credit,
                            @OpeningBal AS Balance,
                            CAST(0 AS BIGINT) AS RowNum,
                            CAST('' AS VARCHAR(20)) AS COCODE

                        UNION ALL

                        -- Transactions between FromDate and ToDate
                        SELECT 
                            1 AS SortOrder,
                            CAST(a.VODATE AS DATE) AS DocDate,
                            ISNULL(a.VONO, '') AS DocNo,
                            ISNULL(a.VOTYPE, '') AS Votype,
                            CASE WHEN a.BILLTINO IS NOT NULL AND a.BILLTINO <> 0 THEN CAST(CAST(a.BILLTINO AS BIGINT) AS VARCHAR(50)) ELSE NULL END AS BillTiNo,
                            CASE WHEN a.BILNO IS NOT NULL AND a.BILNO <> 0 THEN CAST(CAST(a.BILNO AS BIGINT) AS VARCHAR(50)) ELSE NULL END AS BilNo,
                            CASE 
                                WHEN NULLIF(RTRIM(LTRIM(a.VEHICLENO)), '') IS NOT NULL THEN RTRIM(LTRIM(a.VEHICLENO))
                                ELSE COALESCE(
                                    (SELECT TOP 1 ish.VehicleNo FROM ISSHEAD ish WHERE (ish.BillTiNo = a.BILLTINO OR ish.BilNo = a.BILNO) AND NULLIF(RTRIM(LTRIM(ish.VehicleNo)), '') IS NOT NULL),
                                    (SELECT TOP 1 ch.VehicleNo FROM ChallanDet cd INNER JOIN ChallanHead ch ON cd.ChallanHeadId = ch.Id WHERE (cd.BillTiNo = a.BILLTINO OR cd.BilNo = a.BILNO) AND NULLIF(RTRIM(LTRIM(ch.VehicleNo)), '') IS NOT NULL),
                                    (SELECT TOP 1 cm.VehicleNo FROM CommDetail cmd INNER JOIN CommHead cm ON cmd.CommHeadId = cm.Id WHERE cmd.BillTiNo = a.BILLTINO AND NULLIF(RTRIM(LTRIM(cm.VehicleNo)), '') IS NOT NULL),
                                    CASE WHEN a.VOTYPE = 'CL' THEN NULLIF(RTRIM(LTRIM(a.NARRATION)), '') ELSE NULL END,
                                    ''
                                )
                            END AS VehicleNo,
                            ISNULL(a.STATION, '') AS Station,
                            CASE 
                                WHEN a.VOTYPE = 'CL' THEN ISNULL(a.INAME, '')
                                ELSE ISNULL(a.INAME, ISNULL(a.NARRATION, ''))
                            END AS IName,
                            a.QTY AS Qty,
                            ISNULL(a.DRAMT, 0) AS Debit,
                            ISNULL(a.CRAMT, 0) AS Credit,
                            @OpeningBal + SUM(ISNULL(a.DRAMT, 0) - ISNULL(a.CRAMT, 0)) OVER (
                                ORDER BY a.VODATE, a.VONO, (SELECT NULL)
                                ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                            ) AS Balance,
                            ROW_NUMBER() OVER (ORDER BY a.VODATE, a.VONO) AS RowNum,
                            ISNULL(a.COCODE, '') AS COCODE
                        FROM ACCUMULATED a
                        WHERE a.VODATE >= @FromDate AND a.VODATE <= @ToDate
                          AND (@Accode = '' OR RTRIM(LTRIM(ISNULL(a.ACC, ''))) = @Accode OR (RTRIM(LTRIM(ISNULL(a.AC1, ''))) + RTRIM(LTRIM(ISNULL(a.AC3, '')))) = @Accode)
                          AND (
                              @VehicleNo = '' 
                              OR RTRIM(LTRIM(ISNULL(a.VEHICLENO, ''))) = RTRIM(LTRIM(@VehicleNo))
                              OR a.VEHICLENO LIKE '%' + @VehicleNo + '%'
                              OR (a.VOTYPE = 'CL' AND a.NARRATION LIKE '%' + @VehicleNo + '%')
                              OR EXISTS (SELECT 1 FROM ISSHEAD ish WHERE (ish.BillTiNo = a.BILLTINO OR ish.BilNo = a.BILNO) AND ish.VehicleNo LIKE '%' + @VehicleNo + '%')
                              OR EXISTS (SELECT 1 FROM ChallanDet cd INNER JOIN ChallanHead ch ON cd.ChallanHeadId = ch.Id WHERE (cd.BillTiNo = a.BILLTINO OR cd.BilNo = a.BILNO) AND ch.VehicleNo LIKE '%' + @VehicleNo + '%')
                              OR EXISTS (SELECT 1 FROM CommDetail cmd INNER JOIN CommHead cm ON cmd.CommHeadId = cm.Id WHERE cmd.BillTiNo = a.BILLTINO AND cm.VehicleNo LIKE '%' + @VehicleNo + '%')
                          )
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
                        Debit,
                        Credit,
                        Balance,
                        @Accode AS Accode,
                        @AccName AS AccName,
                        @CompanyName AS CompanyName,
                        @FromDate AS FromDate,
                        @ToDate AS ToDate,
                        CASE
                            WHEN COCODE IN ('01','1006') THEN 'W.H'
                            WHEN COCODE IN ('02','1007') THEN 'M.P'
                            WHEN COCODE IN ('03','1008') THEN 'N.K'
                            WHEN COCODE IN ('04','1009') THEN 'R.W'
                            ELSE ISNULL(COCODE,'')
                        END AS RowCompany
                    FROM RawData
                    ORDER BY SortOrder, DocDate, DocNo, RowNum;";

                using (SqlCommand cmdData = new SqlCommand(query, con))
                {
                    cmdData.CommandTimeout = 180;
                    cmdData.Parameters.AddWithValue("@FromDate", fromDate.Date);
                    cmdData.Parameters.AddWithValue("@ToDate", toDate.Date);
                    cmdData.Parameters.AddWithValue("@Accode", string.IsNullOrWhiteSpace(accode) ? "" : accode.Trim());
                    cmdData.Parameters.AddWithValue("@AccName", accName);
                    cmdData.Parameters.AddWithValue("@CompanyName", companyName);
                    cmdData.Parameters.AddWithValue("@CompanyId", companyId);
                    cmdData.Parameters.AddWithValue("@FinancialYearId", financialYearId);
                    cmdData.Parameters.AddWithValue("@VehicleNo", string.IsNullOrWhiteSpace(vehicleNo) ? "" : vehicleNo.Trim());

                    using (SqlDataReader reader = cmdData.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult GeneratePDF(string? accode, DateTime? fromDate, DateTime? toDate, string? vehicleNo, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();
            DateTime effFromDate = fromDate ?? DateTime.Now.AddDays(-30);
            DateTime effToDate = toDate ?? DateTime.Now;

            try
            {
                DataTable dt = GetTransporterLedger(accode ?? "", effFromDate, effToDate, vehicleNo ?? "", selectedCompanyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found for selected date range and transporter account.");
                }

                string reportPath = Path.Combine(_env.WebRootPath, "Reports", "TransporterLedgerrpt.rdlc");

                if (!System.IO.File.Exists(reportPath))
                {
                    return Content($"Report file not found: {reportPath}");
                }

                using (LocalReport report = new LocalReport())
                {
                    report.ReportPath = reportPath;
                    report.DataSources.Clear();

                    dt.TableName = "DSTransporterLedger";
                    report.DataSources.Add(new ReportDataSource("DSTransporterLedger", dt));

                    byte[] pdfBytes = report.Render("PDF");

                    if (pdfBytes == null || pdfBytes.Length < 100)
                    {
                        return Content("PDF generation failed or corrupted output.");
                    }

                    string label = !string.IsNullOrWhiteSpace(accode) ? accode : (!string.IsNullOrWhiteSpace(vehicleNo) ? $"Vehicle_{vehicleNo.Trim()}" : "TransporterLedger");
                    return File(pdfBytes, "application/pdf", $"TransporterLedger_{label}_{effFromDate:yyyyMMdd}_{effToDate:yyyyMMdd}.pdf", enableRangeProcessing: true);
                }
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult OnScreenReport(string? accode, DateTime? fromDate, DateTime? toDate, string? vehicleNo, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();
            DateTime effFromDate = fromDate ?? DateTime.Now.AddDays(-30);
            DateTime effToDate = toDate ?? DateTime.Now;

            try
            {
                DataTable dt = GetTransporterLedger(accode ?? "", effFromDate, effToDate, vehicleNo ?? "", selectedCompanyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No data found for selected date range and transporter account.</strong></div>", "text/html");
                }

                string compName = (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company";
                string accName = (dt.Rows.Count > 0 ? dt.Rows[0]["AccName"]?.ToString() : null) ?? "";

                decimal totalDebit = 0, totalCredit = 0;
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
                    .op-row{background:#e7f1ff;font-weight:bold;}
                    tfoot tr{background:#d0e2ff;font-weight:bold;}
                    " + Nskg.Helpers.ReportPaginationHelper.GetPaginationStyles() + @"
                </style></head><body>");

                sb.Append(Nskg.Helpers.ReportPaginationHelper.GetPaginationToolbarHtml("Transporter Ledger"));
                sb.Append($"<div class='header-box'>");
                sb.Append($"<h2>{compName}</h2>");
                sb.Append($"<h3>TRANSPORTER LEDGER</h3>");
                string vehicleInfo = string.IsNullOrWhiteSpace(vehicleNo) ? "" : $" &nbsp;|&nbsp; Vehicle: <b>{vehicleNo}</b>";
                sb.Append($"<div style='display:flex; justify-content:space-between; align-items:center; margin:8px 0 4px 0; padding-bottom:4px; border-bottom:1.5px solid #000;'>");
                sb.Append($"<div style='font-size:15px; font-weight:bold;'><span style='letter-spacing:1px;'>{accode}</span> &nbsp;&nbsp;&nbsp;&nbsp; <span style='color:#0d6efd;'>{accName}</span></div>");
                sb.Append($"<div style='font-size:11px; color:#495057;'>Period: <b>{effFromDate:dd-MMM-yyyy}</b> to <b>{effToDate:dd-MMM-yyyy}</b>{vehicleInfo}</div>");
                sb.Append($"</div>");
                sb.Append("</div>");

                // Columns exactly matching old software
                sb.Append("<table><thead><tr>");
                sb.Append("<th style='width:75px;'>Doc.Date</th>");
                sb.Append("<th style='width:75px;'>Doc. #</th>");
                sb.Append("<th style='width:40px;'>Typ</th>");
                sb.Append("<th style='width:55px;'>Comm.no</th>");
                sb.Append("<th style='width:90px;'>Vehicle No</th>");
                sb.Append("<th>Station</th>");
                sb.Append("<th style='width:50px;'>Company</th>");
                sb.Append("<th class='num' style='width:85px;'>DEBIT</th>");
                sb.Append("<th class='num' style='width:85px;'>CREDIT</th>");
                sb.Append("<th class='num' style='width:90px;'>BALANCE</th>");
                sb.Append("</tr></thead><tbody>");

                foreach (DataRow row in dt.Rows)
                {
                    int sortOrder = row["SortOrder"] != DBNull.Value ? Convert.ToInt32(row["SortOrder"]) : 1;
                    bool isOpening = sortOrder == 0;

                    decimal debit = row["Debit"] != DBNull.Value ? Convert.ToDecimal(row["Debit"]) : 0;
                    decimal credit = row["Credit"] != DBNull.Value ? Convert.ToDecimal(row["Credit"]) : 0;
                    decimal balance = row["Balance"] != DBNull.Value ? Convert.ToDecimal(row["Balance"]) : 0;

                    if (!isOpening)
                    {
                        totalDebit += debit;
                        totalCredit += credit;
                    }

                    string docDate   = row["DocDate"] != DBNull.Value ? Convert.ToDateTime(row["DocDate"]).ToString("dd-MMM-yy") : "";
                    string votype    = row["Votype"]?.ToString() ?? "";
                    string commNo    = row["BillTiNo"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["BillTiNo"].ToString()) ? row["BillTiNo"].ToString() : "";
                    string vehNo     = row["VehicleNo"]?.ToString() ?? "";
                    string station   = row["Station"]?.ToString() ?? "";
                    string rowComp   = row["RowCompany"] != DBNull.Value ? row["RowCompany"].ToString() : "";
                    string rowClass  = isOpening ? "class='op-row'" : "";

                    if (isOpening)
                    {
                        sb.Append($"<tr {rowClass}>");
                        sb.Append("<td></td><td></td><td></td><td></td>");
                        sb.Append("<td colspan='3' class='bold'>Opening Balance</td>");
                        sb.Append($"<td class='num'>{(debit != 0 ? debit.ToString("#,##0") : "")}</td>");
                        sb.Append($"<td class='num'>{(credit != 0 ? credit.ToString("#,##0") : "")}</td>");
                        sb.Append($"<td class='num bold'>{balance:#,##0}</td>");
                        sb.Append("</tr>");
                    }
                    else
                    {
                        sb.Append($"<tr {rowClass}>");
                        sb.Append($"<td>{docDate}</td>");
                        sb.Append($"<td>{row["DocNo"]}</td>");
                        sb.Append($"<td>{votype}</td>");
                        sb.Append($"<td class='center'>{commNo}</td>");
                        sb.Append($"<td class='bold'>{vehNo}</td>");
                        sb.Append($"<td>{station}</td>");
                        sb.Append($"<td class='center'>{rowComp}</td>");
                        sb.Append($"<td class='num'>{(debit != 0 ? debit.ToString("#,##0") : "")}</td>");
                        sb.Append($"<td class='num'>{(credit != 0 ? credit.ToString("#,##0") : "")}</td>");
                        sb.Append($"<td class='num bold'>{balance:#,##0}</td>");
                        sb.Append("</tr>");
                    }
                }

                decimal closingBal = (dt.Rows.Count > 0 && dt.Rows[dt.Rows.Count - 1]["Balance"] != DBNull.Value) ? Convert.ToDecimal(dt.Rows[dt.Rows.Count - 1]["Balance"]) : 0;

                sb.Append("</tbody><tfoot><tr>");
                sb.Append($"<td colspan='7' class='bold' style='text-align:right;'>GRAND TOTAL............</td>");
                sb.Append($"<td class='num bold'>{totalDebit:#,##0}</td>");
                sb.Append($"<td class='num bold'>{totalCredit:#,##0}</td>");
                sb.Append($"<td class='num bold'>{closingBal:#,##0}</td>");
                sb.Append("</tr></tfoot></table>");
                sb.Append(Nskg.Helpers.ReportPaginationHelper.GetPaginationScript());
                sb.Append("</body></html>");

                return Content(sb.ToString(), "text/html");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult ExportExcel(string? accode, DateTime? fromDate, DateTime? toDate, string? vehicleNo, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            int financialYearId = GetFinancialYearId();
            DateTime effFromDate = fromDate ?? DateTime.Now.AddDays(-30);
            DateTime effToDate = toDate ?? DateTime.Now;

            try
            {
                DataTable dt = GetTransporterLedger(accode ?? "", effFromDate, effToDate, vehicleNo ?? "", selectedCompanyId, financialYearId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                var sb = new StringBuilder();

                // Company Header
                string compName = (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company";
                string accName = (dt.Rows.Count > 0 ? dt.Rows[0]["AccName"]?.ToString() : null) ?? "";
                sb.AppendLine($"\"{compName}\"");
                sb.AppendLine("\"TRANSPORTER LEDGER\"");
                string vehicleInfo = string.IsNullOrWhiteSpace(vehicleNo) ? "" : $",\"Vehicle:\",\"{EscapeCsv(vehicleNo)}\"";
                sb.AppendLine($"\"Account:\",\"{accode} - {accName}\",\"From:\",\"{effFromDate:dd-MM-yyyy}\",\"To:\",\"{effToDate:dd-MM-yyyy}\"{vehicleInfo}");
                sb.AppendLine();

                // Table Header
                sb.AppendLine("Doc.Date,Doc. #,Typ,BilltiNo,Bill. No,Vehicle No,Station,Party name / Item,Qty,DEBIT,CREDIT,BALANCE");

                foreach (DataRow row in dt.Rows)
                {
                    string docDate = row["DocDate"] != DBNull.Value && row["DocDate"] != null ? Convert.ToDateTime(row["DocDate"]).ToString("dd-MM-yyyy") : "";
                    string docNo = EscapeCsv(row["DocNo"]?.ToString() ?? "");
                    string votype = EscapeCsv(row["Votype"]?.ToString() ?? "");
                    string billtiNo = EscapeCsv(row["BillTiNo"]?.ToString() ?? "");
                    string bilNo = EscapeCsv(row["BilNo"]?.ToString() ?? "");
                    string vNo = EscapeCsv(row["VehicleNo"]?.ToString() ?? "");
                    string station = EscapeCsv(row["Station"]?.ToString() ?? "");
                    string iname = EscapeCsv(row["IName"]?.ToString() ?? "");
                    string qty = row["Qty"] != DBNull.Value && row["Qty"] != null ? (row["Qty"]?.ToString() ?? "") : "";
                    string debit = row["Debit"] != DBNull.Value && Convert.ToDecimal(row["Debit"]) != 0 ? Convert.ToDecimal(row["Debit"]).ToString("F2") : "";
                    string credit = row["Credit"] != DBNull.Value && Convert.ToDecimal(row["Credit"]) != 0 ? Convert.ToDecimal(row["Credit"]).ToString("F2") : "";
                    string balance = row["Balance"] != DBNull.Value ? Convert.ToDecimal(row["Balance"]).ToString("F2") : "";

                    sb.AppendLine($"{docDate},{docNo},{votype},{billtiNo},{bilNo},{vNo},{station},{iname},{qty},{debit},{credit},{balance}");
                }

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                string vehicleSuffix = string.IsNullOrWhiteSpace(vehicleNo) ? "" : $"_{vehicleNo.Trim()}";
                return File(bytes, "text/csv", $"TransporterLedger_{accode}{vehicleSuffix}_{effFromDate:yyyyMMdd}_{effToDate:yyyyMMdd}.csv");
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
