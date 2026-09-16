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
        public IActionResult Index(string accode, string fromDate, string toDate, string vehicleNo)
        {
            int companyId = GetCompanyId();

            var accounts = _context.GLChart3
                .Where(x => x.CompanyId == companyId || x.CompanyId == 0 || x.CompanyId == null)
                .Select(x => new
                {
                    Code = x.ACC ?? (x.AC1 + x.AC3),
                    Name = x.Name
                })
                .OrderBy(x => x.Code)
                .ToList();

            var vehicles = _context.VoDet
                .Where(v => v.Vehicleno != null && v.Vehicleno.Trim() != "" && (v.VoHead == null || v.VoHead.CompanyId == companyId || v.VoHead.CompanyId == 0))
                .Select(v => v.Vehicleno!.Trim())
                .Union(_context.ChallanHead
                    .Where(c => c.VehicleNo != null && c.VehicleNo.Trim() != "" && (c.CompanyId == companyId || c.CompanyId == 0))
                    .Select(c => c.VehicleNo!.Trim()))
                .Union(_context.CommHead
                    .Where(c => c.VehicleNo != null && c.VehicleNo.Trim() != "" && (c.CompanyId == companyId || c.CompanyId == 0))
                    .Select(c => c.VehicleNo!.Trim()))
                .Where(v => v != "")
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            ViewBag.AccountList = accounts;
            ViewBag.VehicleList = vehicles;
            ViewBag.Accode = accode;
            ViewBag.VehicleNo = vehicleNo;
            ViewBag.FromDate = string.IsNullOrEmpty(fromDate) ? DateTime.Now.ToString("yyyy-MM-dd") : fromDate;
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

                // 1. Run dbo.PROCESSDETAIL_Transporter to populate ACCUMULATED & ACCOPEN
                using (SqlCommand cmdProc = new SqlCommand("dbo.PROCESSDETAIL_Transporter", con))
                {
                    cmdProc.CommandType = CommandType.StoredProcedure;
                    cmdProc.CommandTimeout = 180;
                    cmdProc.Parameters.AddWithValue("@CompanyId", companyId);
                    cmdProc.Parameters.AddWithValue("@TDATE", toDate.Date);
                    cmdProc.Parameters.AddWithValue("@ACCODE", accode?.Trim() ?? "");
                    cmdProc.ExecuteNonQuery();
                }

                // 2. Fetch Account Name & Company Name
                string accName = "";
                using (SqlCommand cmdAcc = new SqlCommand(
                    "SELECT TOP 1 Name FROM GLCHART3 WHERE (CompanyId = @CompanyId OR CompanyId = 0 OR CompanyId IS NULL) AND (RTRIM(AC1) + RTRIM(AC3) = RTRIM(@Accode) OR ACC = RTRIM(@Accode))", con))
                {
                    cmdAcc.Parameters.AddWithValue("@CompanyId", companyId);
                    cmdAcc.Parameters.AddWithValue("@Accode", accode?.Trim() ?? "");
                    var res = cmdAcc.ExecuteScalar();
                    if (res != null && res != DBNull.Value) accName = res.ToString();
                }

                string companyName = "West Wharf-New Shadab Karachi Goods Transports";
                using (SqlCommand cmdComp = new SqlCommand("SELECT TOP 1 Name FROM Companies WHERE Id = @CompanyId", con))
                {
                    cmdComp.Parameters.AddWithValue("@CompanyId", companyId);
                    var res = cmdComp.ExecuteScalar();
                    if (res != null && res != DBNull.Value && !string.IsNullOrWhiteSpace(res.ToString()))
                        companyName = res.ToString();
                }

                // 3. Query Opening Balance and Transactions with Running Balance
                string query = @"
                    DECLARE @AnnualOpeningBal DECIMAL(18,2) = 0;

                    IF OBJECT_ID('OpeningBalances', 'U') IS NOT NULL
                    BEGIN
                        SELECT @AnnualOpeningBal = ISNULL(SUM(Debit - Credit), 0)
                        FROM OpeningBalances
                        WHERE Accode = @Accode
                          AND CompanyId = @CompanyId
                          AND FinancialYearId = @FinancialYearId;
                    END

                    DECLARE @OpeningBal DECIMAL(18,2) = 0;

                    SELECT @OpeningBal = @AnnualOpeningBal + ISNULL(SUM(ISNULL(DRAMT, 0) - ISNULL(CRAMT, 0)), 0)
                    FROM ACCUMULATED
                    WHERE VODATE < @FromDate
                      AND (@VehicleNo = '' OR RTRIM(LTRIM(ISNULL(VEHICLENO, ''))) = RTRIM(LTRIM(@VehicleNo)));

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
                            CAST(0 AS BIGINT) AS RowNum

                        UNION ALL

                        -- Transactions between FromDate and ToDate
                        SELECT 
                            1 AS SortOrder,
                            CAST(VODATE AS DATE) AS DocDate,
                            ISNULL(VONO, '') AS DocNo,
                            ISNULL(VOTYPE, '') AS Votype,
                            CASE WHEN BILLTINO IS NOT NULL AND BILLTINO <> 0 THEN CAST(CAST(BILLTINO AS BIGINT) AS VARCHAR(50)) ELSE NULL END AS BillTiNo,
                            CASE WHEN BILNO IS NOT NULL AND BILNO <> 0 THEN CAST(CAST(BILNO AS BIGINT) AS VARCHAR(50)) ELSE NULL END AS BilNo,
                            ISNULL(VEHICLENO, '') AS VehicleNo,
                            ISNULL(STATION, '') AS Station,
                            ISNULL(INAME, ISNULL(NARRATION, '')) AS IName,
                            QTY AS Qty,
                            ISNULL(DRAMT, 0) AS Debit,
                            ISNULL(CRAMT, 0) AS Credit,
                            @OpeningBal + SUM(ISNULL(DRAMT, 0) - ISNULL(CRAMT, 0)) OVER (
                                ORDER BY VODATE, VONO, (SELECT NULL)
                                ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                            ) AS Balance,
                            ROW_NUMBER() OVER (ORDER BY VODATE, VONO) AS RowNum
                        FROM ACCUMULATED
                        WHERE VODATE >= @FromDate AND VODATE <= @ToDate
                          AND (@VehicleNo = '' OR RTRIM(LTRIM(ISNULL(VEHICLENO, ''))) = RTRIM(LTRIM(@VehicleNo)))
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
        public IActionResult GeneratePDF(string accode, DateTime fromDate, DateTime toDate, string vehicleNo)
        {
            int companyId = GetCompanyId();
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetTransporterLedger(accode, fromDate, toDate, vehicleNo, companyId, financialYearId);

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

                    string vehicleSuffix = string.IsNullOrWhiteSpace(vehicleNo) ? "" : $"_{vehicleNo.Trim()}";
                    return File(pdfBytes, "application/pdf", $"TransporterLedger_{accode}{vehicleSuffix}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf", enableRangeProcessing: true);
                }
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult OnScreenReport(string accode, DateTime fromDate, DateTime toDate, string vehicleNo)
        {
            int companyId = GetCompanyId();
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetTransporterLedger(accode, fromDate, toDate, vehicleNo, companyId, financialYearId);

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
        public IActionResult ExportExcel(string accode, DateTime fromDate, DateTime toDate, string vehicleNo)
        {
            int companyId = GetCompanyId();
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetTransporterLedger(accode, fromDate, toDate, vehicleNo, companyId, financialYearId);

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
                sb.AppendLine($"\"Account:\",\"{accode} - {accName}\",\"From:\",\"{fromDate:dd-MM-yyyy}\",\"To:\",\"{toDate:dd-MM-yyyy}\"{vehicleInfo}");
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
                return File(bytes, "text/csv", $"TransporterLedger_{accode}{vehicleSuffix}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv");
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
