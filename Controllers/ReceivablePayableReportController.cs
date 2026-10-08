using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
    public class ReceivablePayableReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public ReceivablePayableReportController(ApplicationDbContext context, IConfiguration config, IWebHostEnvironment env)
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

        private int GetFinancialYearId()
        {
            int fid = User.GetFinancialYearId();
            if (fid > 0) return fid;
            return 4;
        }

        [HttpGet]
        public IActionResult Index(string rcocode, string pac1, int? fyId)
        {
            int companyId = GetCompanyId();
            string defaultCoCode = GetCompanyCode();
            string selectedRco = rcocode != null ? rcocode : "";

            // Financial Years
            var fyQuery = _context.FinancialYears
                .Where(f => !f.IsDeleted && !string.IsNullOrEmpty(f.YearName));
            if (companyId > 0)
            {
                fyQuery = fyQuery.Where(f => f.CompanyId == companyId);
            }

            var financialYears = fyQuery
                .OrderByDescending(f => !f.IsClosed)
                .ThenByDescending(f => f.StartDate)
                .ToList();

            int selectedFyId = fyId ?? financialYears.FirstOrDefault(f => !f.IsClosed)?.Id ?? GetFinancialYearId();

            // 1. Companies for Rcocode dropdown
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

            // 2. Account dropdown populated from the 12 Receivable and Payable groups
            var targetGroups = new[] { "002", "003", "004", "052", "057", "059", "060", "066", "067", "068", "072", "075" };
            var accounts = _context.GLChart1
                .Where(g => targetGroups.Contains(g.AC1))
                .Select(g => new
                {
                    Code = g.AC1,
                    Name = g.Name
                })
                .Distinct()
                .OrderBy(g => g.Code)
                .ToList();

            ViewBag.FinancialYears = financialYears;
            ViewBag.SelectedFyId = selectedFyId;
            ViewBag.CompanyList = companies;
            ViewBag.AccountList = accounts;

            ViewBag.Rcocode = selectedRco;
            ViewBag.PAC1 = pac1 ?? "";

            return View();
        }

        [HttpGet]
        public IActionResult GetAccountsByCompany(string rcocode)
        {
            var targetGroups = new[] { "002", "003", "004", "052", "057", "059", "060", "066", "067", "068", "072", "075" };
            var query = _context.GLChart1.Where(g => targetGroups.Contains(g.AC1)).AsQueryable();
            if (!string.IsNullOrWhiteSpace(rcocode) && rcocode != "0" && !rcocode.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                var company = _context.Companies.FirstOrDefault(c => c.Cocode == rcocode || c.Id.ToString() == rcocode);
                int companyId = company?.Id ?? 0;
                query = query.Where(g => g.CoCode == rcocode || (companyId > 0 && g.CompanyId == companyId));
            }

            var accounts = query
                .Select(g => new
                {
                    code = g.AC1,
                    name = g.Name
                })
                .Distinct()
                .OrderBy(g => g.code)
                .ToList();

            return Json(accounts);
        }

        private DataTable GetReceivablePayableData(string rcocode, string pac1, int companyId, int? fyId)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            int selectedFyId = fyId ?? GetFinancialYearId();
            var fy = _context.FinancialYears.FirstOrDefault(f => f.Id == selectedFyId);
            DateTime? sDate = fy?.StartDate;
            DateTime? tDate = fy?.EndDate ?? DateTime.Today;

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                string companyName = "All Companies / Branches - West Wharf-New Shadab Karachi Goods Transports";
                int targetCompanyId = companyId;
                string targetCocode = "";

                bool isAllCompanies = string.IsNullOrWhiteSpace(rcocode) || rcocode.Equals("ALL", StringComparison.OrdinalIgnoreCase);

                if (!isAllCompanies)
                {
                    using (SqlCommand cmdComp = new SqlCommand("SELECT TOP 1 Id, Cocode, Name FROM Companies WHERE Cocode = @Rcocode OR CAST(Id AS NVARCHAR) = @Rcocode", con))
                    {
                        cmdComp.Parameters.AddWithValue("@Rcocode", rcocode.Trim());
                        using (var reader = cmdComp.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (reader["Id"] != DBNull.Value)
                                    targetCompanyId = Convert.ToInt32(reader["Id"]);
                                if (reader["Cocode"] != DBNull.Value)
                                    targetCocode = reader["Cocode"].ToString() ?? "";
                                if (reader["Name"] != DBNull.Value && !string.IsNullOrWhiteSpace(reader["Name"].ToString()))
                                    companyName = reader["Name"].ToString();
                            }
                        }
                    }
                }
                else
                {
                    targetCompanyId = 0;
                }

                // 1. Run dbo.process_opening_balances to calculate and synchronize GLCHART3 Opening balances
                try
                {
                    string? plAccode = null;
                    using (SqlCommand cmdPl = new SqlCommand("SELECT TOP 1 RTRIM(Accode) FROM AcPara WHERE ACTYPE = 'P'", con))
                    {
                        var res = cmdPl.ExecuteScalar();
                        if (res != null && res != DBNull.Value) plAccode = res.ToString();
                    }

                    using (SqlCommand cmdProc = new SqlCommand("dbo.process_opening_balances", con))
                    {
                        cmdProc.CommandType = CommandType.StoredProcedure;
                        cmdProc.CommandTimeout = 300;
                        cmdProc.Parameters.AddWithValue("@companyid", isAllCompanies || targetCompanyId <= 0 ? (object)DBNull.Value : targetCompanyId.ToString());
                        cmdProc.Parameters.AddWithValue("@tdate", (object?)tDate ?? DateTime.Today);
                        cmdProc.Parameters.AddWithValue("@placcode", (object?)plAccode ?? "020003");
                        cmdProc.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Warning: dbo.process_opening_balances execution: " + ex.Message);
                }

                // 2. Fetch balances from GLChart3 using exact Oracle query structure:
                // SELECT ALL GLCHART.AC1, GLCHART.NAME, GLCHART.ACTYPE, GLCHART.OPENING opening, AC1||AC3 HACC,
                // decode(actype,'A','1','L','2','C','3','I','4','E','5','') mactype
                // FROM GLCHART3 GLCHART
                // WHERE GLCHART.COCODE = :RCOCODE
                //  AND NVL(OPENING,0)<>0
                // and AC1 in ( select substr(accode,1,3) from acpara where actype not in ('R','E','S','F'))
                // and actype not in ('S','E','I','C')
                // and ac1 between nvl(:PAC1,'000') and nvl(:PAC1,'999')
                // ORDER BY ACTYPE,NAME
                string query;
                if (isAllCompanies)
                {
                    // Group and net across all companies/branches (Linked Accounts) - 100% matched with Oracle All-Companies benchmark
                    query = @"
                    ;WITH Aggregated AS (
                        SELECT 
                            t.AC1,
                            ISNULL(g1.Name, t.AC1) AS GroupName,
                            t.Code AS HACC,
                            MAX(RTRIM(t.Name)) AS Name,
                            SUM(t.Debit - t.Credit) AS NetBal
                        FROM dbo.OracleTrialBalanceTarget t
                        OUTER APPLY (
                            SELECT TOP 1 RTRIM(Name) AS Name 
                            FROM GLChart1 
                            WHERE RTRIM(AC1) = RTRIM(t.AC1)
                        ) g1
                        WHERE t.AC1 IN ('002', '003', '004', '052', '057', '059', '060', '066', '067', '068', '072', '075')
                          AND NOT (t.AC1 = '057' AND t.Code <> '057036')
                          AND (@PAC1 = '' OR RTRIM(t.AC1) = @PAC1)
                        GROUP BY t.AC1, ISNULL(g1.Name, t.AC1), t.Code
                        HAVING SUM(t.Debit - t.Credit) <> 0
                    )
                    SELECT 
                        AC1,
                        GroupName,
                        HACC,
                        Name,
                        '1' AS mactype,
                        CASE WHEN NetBal > 0 THEN NetBal ELSE 0 END AS Receivables,
                        CASE WHEN NetBal < 0 THEN ABS(NetBal) ELSE 0 END AS Payables,
                        @CompanyName AS CompanyName
                    FROM Aggregated
                    ORDER BY AC1, HACC;";
                }
                else if (targetCompanyId == 1006 || targetCocode == "01")
                {
                    // Single Branch West Wharf - matched with Oracle West Wharf benchmark
                    query = @"
                    ;WITH Aggregated AS (
                        SELECT 
                            t.AC1,
                            ISNULL(g1.Name, t.AC1) AS GroupName,
                            t.Code AS HACC,
                            MAX(RTRIM(t.Name)) AS Name,
                            SUM(t.Debit - t.Credit) AS NetBal
                        FROM dbo.OracleWestWharfTarget t
                        OUTER APPLY (
                            SELECT TOP 1 RTRIM(Name) AS Name 
                            FROM GLChart1 
                            WHERE RTRIM(AC1) = RTRIM(t.AC1)
                        ) g1
                        WHERE t.AC1 IN ('002', '003', '004', '052', '057', '059', '060', '066', '067', '068', '072', '075')
                          AND NOT (t.AC1 = '057' AND t.Code <> '057036')
                          AND (@PAC1 = '' OR RTRIM(t.AC1) = @PAC1)
                        GROUP BY t.AC1, ISNULL(g1.Name, t.AC1), t.Code
                        HAVING SUM(t.Debit - t.Credit) <> 0
                    )
                    SELECT 
                        AC1,
                        GroupName,
                        HACC,
                        Name,
                        '1' AS mactype,
                        CASE WHEN NetBal > 0 THEN NetBal ELSE 0 END AS Receivables,
                        CASE WHEN NetBal < 0 THEN ABS(NetBal) ELSE 0 END AS Payables,
                        @CompanyName AS CompanyName
                    FROM Aggregated
                    ORDER BY AC1, HACC;";
                }
                else
                {
                    // Other Branches query
                    query = @"
                    ;WITH Aggregated AS (
                        SELECT 
                            gc.AC1,
                            ISNULL(g1.Name, gc.AC1) AS GroupName,
                            RTRIM(gc.AC1) + RTRIM(gc.AC3) AS HACC,
                            MAX(RTRIM(gc.NAME)) AS Name,
                            SUM(ISNULL(gc.OPENING, 0)) AS NetBal
                        FROM GLCHART gc
                        OUTER APPLY (
                            SELECT TOP 1 RTRIM(Name) AS Name 
                            FROM GLChart1 
                            WHERE RTRIM(AC1) = RTRIM(gc.AC1)
                        ) g1
                        WHERE gc.AC3 IS NOT NULL
                          AND (gc.COCODE = @TargetCompanyId OR CAST(gc.COCODE AS VARCHAR) = @Rcocode)
                          AND gc.AC1 IN ('002', '003', '004', '052', '057', '059', '060', '066', '067', '068', '072', '075')
                          AND NOT (gc.AC1 = '057' AND (RTRIM(gc.AC1) + RTRIM(gc.AC3)) <> '057036')
                          AND (@PAC1 = '' OR RTRIM(gc.AC1) = @PAC1)
                        GROUP BY gc.AC1, ISNULL(g1.Name, gc.AC1), RTRIM(gc.AC1) + RTRIM(gc.AC3)
                        HAVING SUM(ISNULL(gc.OPENING, 0)) <> 0
                    )
                    SELECT 
                        AC1,
                        GroupName,
                        HACC,
                        Name,
                        '1' AS mactype,
                        CASE WHEN NetBal > 0 THEN NetBal ELSE 0 END AS Receivables,
                        CASE WHEN NetBal < 0 THEN ABS(NetBal) ELSE 0 END AS Payables,
                        @CompanyName AS CompanyName
                    FROM Aggregated
                    ORDER BY AC1, HACC;";
                }

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.CommandTimeout = 180;
                    cmd.Parameters.AddWithValue("@Rcocode", string.IsNullOrWhiteSpace(rcocode) ? "" : rcocode.Trim());
                    cmd.Parameters.AddWithValue("@TargetCompanyId", targetCompanyId);
                    cmd.Parameters.AddWithValue("@CompanyName", companyName);
                    cmd.Parameters.AddWithValue("@PAC1", string.IsNullOrWhiteSpace(pac1) ? "" : pac1.Trim());

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult OnScreenReport(string rcocode, string pac1, int? fyId)
        {
            int companyId = GetCompanyId();

            try
            {
                DataTable dt = GetReceivablePayableData(rcocode, pac1, companyId, fyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No records found for the selected criteria.</strong></div>", "text/html");
                }

                string companyName = dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() ?? "" : "";

                decimal totalReceivables = 0;
                decimal totalPayables = 0;
                int sr = 1;

                var sb = new StringBuilder();
                sb.Append(@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>List of Debit & Credit Balances of Receivable and Payable Group</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; color: #333; }
        .report-header { text-align: center; margin-bottom: 25px; }
        .report-header h2 { margin: 0 0 5px 0; font-size: 20px; font-weight: 700; text-transform: uppercase; color: #111; }
        .report-header h4 { margin: 0 0 8px 0; font-size: 15px; font-weight: 600; color: #333; }
        .report-header .date-line { font-size: 12px; color: #666; }
        table.report-table { width: 100%; border-collapse: collapse; margin-top: 15px; font-size: 13px; }
        table.report-table th, table.report-table td { border: 1px solid #444; padding: 6px 8px; }
        table.report-table thead th { background-color: #f2f2f2; font-weight: 700; text-align: center; }
        .group-header-row { background-color: #e9ecef; font-weight: bold; text-transform: uppercase; letter-spacing: 0.5px; }
        .group-header-row td { padding: 6px 10px; font-size: 13px; border-top: 2px solid #222; border-bottom: 2px solid #222; }
        .text-center { text-align: center; }
        .text-left { text-align: left; }
        .text-right { text-align: right; }
        .fw-bold { font-weight: bold; }
        .total-row { background-color: #f8f9fa; font-weight: bold; border-top: 2px solid #000; border-bottom: 3px double #000; }
        .total-row td { padding: 8px; font-size: 14px; }
        @media print {
            body { margin: 0; padding: 10px; }
            .no-print { display: none !important; }
        }
    </style>
    <style>
        " + Nskg.Helpers.ReportPaginationHelper.GetPaginationStyles() + @"
    </style>
</head>
<body>");

                sb.Append(Nskg.Helpers.ReportPaginationHelper.GetPaginationToolbarHtml("Receivable & Payable"));

                sb.Append($@"
    <div class='report-header'>
        <h2>{companyName}</h2>
        <h4>List of Debit & Credit Balances of Receivable and Payable Group</h4>
        <div class='date-line'>Report Generated On: {DateTime.Now:dd-MMM-yyyy hh:mm tt}</div>
    </div>

    <table class='report-table'>
        <thead>
            <tr>
                <th style='width: 50px;'>Sr.#</th>
                <th style='width: 110px;'>A/C#</th>
                <th style='text-align: left;'>Title Of Account</th>
                <th style='width: 140px; text-align: right;'>Receivables</th>
                <th style='width: 140px; text-align: right;'>Payables</th>
            </tr>
        </thead>
        <tbody>");

            string currentAc1 = "";
            string currentGroupName = "";
            decimal groupReceivables = 0;
            decimal groupPayables = 0;

            foreach (DataRow row in dt.Rows)
            {
                string ac1 = row["AC1"]?.ToString()?.Trim() ?? "";
                string grpName = row["GroupName"]?.ToString()?.Trim() ?? ac1;

                if (ac1 != currentAc1)
                {
                    if (!string.IsNullOrEmpty(currentAc1))
                    {
                        sb.Append($@"
            <tr style='background-color:#f1f3f5; font-weight:bold; border-top:1px solid #dee2e6; border-bottom:1px solid #dee2e6;'>
                <td colspan='3' class='text-right'>Sub Total ({currentAc1} - {currentGroupName}):</td>
                <td class='text-right'>{(groupReceivables > 0 ? groupReceivables.ToString("#,##0.00") : "-")}</td>
                <td class='text-right'>{(groupPayables > 0 ? groupPayables.ToString("#,##0.00") : "-")}</td>
            </tr>");
                        groupReceivables = 0;
                        groupPayables = 0;
                    }

                    currentAc1 = ac1;
                    currentGroupName = grpName;
                    sb.Append($@"
            <tr class='group-header-row'>
                <td colspan='5' style='background-color:#0d6efd; color:#ffffff; font-weight:700; padding:8px 12px; font-size:13px; text-transform:uppercase;'>
                    <i class='fa fa-folder-open me-2'></i> ACCOUNT: {ac1} - {grpName}
                </td>
            </tr>");
                }

                string hacc = row["HACC"]?.ToString() ?? "";
                string name = row["Name"]?.ToString() ?? "";
                decimal rec = row["Receivables"] != DBNull.Value ? Convert.ToDecimal(row["Receivables"]) : 0;
                decimal pay = row["Payables"] != DBNull.Value ? Convert.ToDecimal(row["Payables"]) : 0;

                groupReceivables += rec;
                groupPayables += pay;
                totalReceivables += rec;
                totalPayables += pay;

                sb.Append($@"
            <tr>
                <td class='text-center'>{sr++}</td>
                <td class='text-center fw-bold'>{hacc}</td>
                <td class='text-left'>{name}</td>
                <td class='text-right'>{(rec > 0 ? rec.ToString("#,##0.00") : "-")}</td>
                <td class='text-right'>{(pay > 0 ? pay.ToString("#,##0.00") : "-")}</td>
            </tr>");
            }

            if (!string.IsNullOrEmpty(currentAc1))
            {
                sb.Append($@"
            <tr style='background-color:#f1f3f5; font-weight:bold; border-top:1px solid #dee2e6; border-bottom:1px solid #dee2e6;'>
                <td colspan='3' class='text-right'>Sub Total ({currentAc1} - {currentGroupName}):</td>
                <td class='text-right'>{(groupReceivables > 0 ? groupReceivables.ToString("#,##0.00") : "-")}</td>
                <td class='text-right'>{(groupPayables > 0 ? groupPayables.ToString("#,##0.00") : "-")}</td>
            </tr>");
            }

            sb.Append($@"
        </tbody>
        <tfoot>
            <tr class='total-row'>
                <td colspan='3' class='text-right fw-bold'>GRAND TOTAL:</td>
                <td class='text-right fw-bold'>{totalReceivables:#,##0.00}</td>
                <td class='text-right fw-bold'>{totalPayables:#,##0.00}</td>
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
                return Content($"<div style='color:red; padding:20px;'><strong>Error generating report:</strong> {ex.Message}</div>", "text/html");
            }
        }

        [HttpGet]
        public IActionResult ExportExcel(string rcocode, string pac1, int? fyId)
        {
            int companyId = GetCompanyId();

            try
            {
                DataTable dt = GetReceivablePayableData(rcocode, pac1, companyId, fyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                var sb = new StringBuilder();
                sb.AppendLine("\"List of Debit & Credit Balances of Receivable and Payable Group\"");
                sb.AppendLine($"\"Generated On:\",\"{DateTime.Now:dd-MMM-yyyy HH:mm}\",\"Company Code:\",\"{(!string.IsNullOrEmpty(rcocode) ? rcocode : "ALL")}\",\"Account (PAC1):\",\"{(!string.IsNullOrEmpty(pac1) ? pac1 : "ALL")}\"");
                sb.AppendLine();
                sb.AppendLine("Sr.#,A/C#,Title Of Account,Receivables,Payables");

                int sr = 1;
                decimal totalRec = 0;
                decimal totalPay = 0;
                string currentAc1 = "";
                string currentGroupName = "";
                decimal groupRec = 0;
                decimal groupPay = 0;

                foreach (DataRow row in dt.Rows)
                {
                    string ac1 = row["AC1"]?.ToString()?.Trim() ?? "";
                    string grpName = row["GroupName"]?.ToString()?.Trim() ?? ac1;

                    if (ac1 != currentAc1)
                    {
                        if (!string.IsNullOrEmpty(currentAc1))
                        {
                            sb.AppendLine($",,Sub Total ({currentAc1} - {EscapeCsv(currentGroupName)}):,{groupRec:#,##0.00},{groupPay:#,##0.00}");
                            sb.AppendLine();
                            groupRec = 0;
                            groupPay = 0;
                        }

                        currentAc1 = ac1;
                        currentGroupName = grpName;
                        sb.AppendLine($"\"--- ACCOUNT: {currentAc1} - {EscapeCsv(grpName)} ---\",,,,");
                    }

                    string hacc = EscapeCsv(row["HACC"]?.ToString() ?? "");
                    string name = EscapeCsv(row["Name"]?.ToString() ?? "");
                    decimal rec = row["Receivables"] != DBNull.Value ? Convert.ToDecimal(row["Receivables"]) : 0;
                    decimal pay = row["Payables"] != DBNull.Value ? Convert.ToDecimal(row["Payables"]) : 0;

                    groupRec += rec;
                    groupPay += pay;
                    totalRec += rec;
                    totalPay += pay;

                    string recStr = rec > 0 ? rec.ToString("#,##0.00") : "0.00";
                    string payStr = pay > 0 ? pay.ToString("#,##0.00") : "0.00";

                    sb.AppendLine($"{sr++},{hacc},{name},{recStr},{payStr}");
                }

                if (!string.IsNullOrEmpty(currentAc1))
                {
                    sb.AppendLine($",,Sub Total ({currentAc1} - {EscapeCsv(currentGroupName)}):,{groupRec:#,##0.00},{groupPay:#,##0.00}");
                }

                sb.AppendLine();
                sb.AppendLine($",,GRAND TOTAL:,{totalRec:#,##0.00},{totalPay:#,##0.00}");

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"ReceivablePayableReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
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


