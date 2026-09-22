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
            return 1006;
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
        public IActionResult Index(string rcocode, string pac1)
        {
            int companyId = GetCompanyId();
            string defaultCoCode = GetCompanyCode();
            string selectedRco = !string.IsNullOrEmpty(rcocode) ? rcocode : defaultCoCode;

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

            // 2. Account dropdown populated directly from GLChart1 (containing CASH, CAPITAL, SERVICES, EXPENSES, RUQQA, etc.)
            var accounts = _context.GLChart1
                .Where(g => (g.CoCode == selectedRco || g.CompanyId == companyId || (selectedRco == "" && g.CompanyId == 0)))
                .Select(g => new
                {
                    Code = g.AC1,
                    Name = g.Name
                })
                .Distinct()
                .OrderBy(g => g.Name)
                .ToList();

            ViewBag.CompanyList = companies;
            ViewBag.AccountList = accounts;

            ViewBag.Rcocode = selectedRco;
            ViewBag.PAC1 = pac1 ?? "";

            return View();
        }

        [HttpGet]
        public IActionResult GetAccountsByCompany(string rcocode)
        {
            if (string.IsNullOrWhiteSpace(rcocode)) rcocode = "01";

            var company = _context.Companies.FirstOrDefault(c => c.Cocode == rcocode || c.Id.ToString() == rcocode);
            int companyId = company?.Id ?? 0;

            var accounts = _context.GLChart1
                .Where(g => g.CoCode == rcocode || (companyId > 0 && g.CompanyId == companyId))
                .Select(g => new
                {
                    code = g.AC1,
                    name = g.Name
                })
                .Distinct()
                .OrderBy(g => g.name)
                .ToList();

            return Json(accounts);
        }

        private DataTable GetReceivablePayableData(string rcocode, string pac1, int companyId, int financialYearId)
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

                // Translated Oracle Query:
                // SELECT ALL GLCHART.AC1, GLCHART.NAME, GLCHART.ACTYPE, GLCHART.OPENING opening, AC1||AC3 HACC,
                // decode(actype,'A','1','L','2','C','3','I','4','E','5','') mactype
                // FROM GLCHART3 GLCHART
                // WHERE GLCHART.COCODE = :RCOCODE
                //  AND NVL(OPENING,0)<>0
                // and AC1 in ( select substr(accode,1,3) from acpara where actype not in ('R','E','S','F'))
                // and actype not in ('S','E','I','C')
                // and ac1 between nvl(:PAC1,'000') and nvl(:PAC1,'999')
                // ORDER BY ACTYPE,NAME
                string query = @"
                    SELECT 
                        GLCHART.AC1,
                        GLCHART.Name,
                        GLCHART.AcType,
                        ISNULL(GLCHART.Opening, 0) AS Opening,
                        ISNULL(GLCHART.ACC, RTRIM(LTRIM(ISNULL(GLCHART.AC1, ''))) + RTRIM(LTRIM(ISNULL(GLCHART.AC3, '')))) AS HACC,
                        CASE GLCHART.AcType 
                            WHEN 'A' THEN '1' 
                            WHEN 'L' THEN '2' 
                            WHEN 'C' THEN '3' 
                            WHEN 'I' THEN '4' 
                            WHEN 'E' THEN '5' 
                            ELSE '' 
                        END AS mactype,
                        CASE WHEN ISNULL(GLCHART.Opening, 0) > 0 THEN ISNULL(GLCHART.Opening, 0) ELSE 0 END AS Receivables,
                        CASE WHEN ISNULL(GLCHART.Opening, 0) < 0 THEN ABS(ISNULL(GLCHART.Opening, 0)) ELSE 0 END AS Payables,
                        @CompanyName AS CompanyName
                    FROM GLChart3 GLCHART
                    WHERE (
                            (@Rcocode <> '' AND (GLCHART.CoCode = @Rcocode OR GLCHART.CompanyId = @TargetCompanyId))
                            OR (@Rcocode = '' AND (GLCHART.CompanyId = @TargetCompanyId OR @TargetCompanyId = 0 OR GLCHART.CompanyId IS NULL))
                          )
                      AND ISNULL(GLCHART.Opening, 0) <> 0
                      AND (
                          -- If specific PAC1 (GLChart1 Account AC1) is selected
                          (@PAC1 <> '' AND GLCHART.AC1 = @PAC1)
                          OR
                          -- If PAC1 is not selected (ALL): AC1 in AcPara (actype not in R, E, S, F) and GLCHART.AcType not in S, E, I, C
                          (@PAC1 = '' AND (
                              GLCHART.AC1 IN (
                                  SELECT DISTINCT SUBSTRING(Accode, 1, 3) 
                                  FROM AcPara 
                                  WHERE (CompanyId = @TargetCompanyId OR @TargetCompanyId = 0 OR CompanyId IS NULL OR Cocode = @Rcocode)
                                    AND ACTYPE NOT IN ('R', 'E', 'S', 'F')
                              )
                              AND (GLCHART.AcType IS NULL OR GLCHART.AcType NOT IN ('S', 'E', 'I', 'C'))
                          ))
                      )
                    ORDER BY GLCHART.AcType, GLCHART.Name;";

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
        public IActionResult OnScreenReport(string rcocode, string pac1)
        {
            int companyId = GetCompanyId();
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetReceivablePayableData(rcocode, pac1, companyId, financialYearId);

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
</head>
<body>");

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
        <tbody>
            <tr class='group-header-row'>
                <td colspan='5'>RECEIVABLE & PAYABLE BALANCES</td>
            </tr>");

                foreach (DataRow row in dt.Rows)
                {
                    string hacc = row["HACC"]?.ToString() ?? "";
                    string name = row["Name"]?.ToString() ?? "";
                    decimal rec = row["Receivables"] != DBNull.Value ? Convert.ToDecimal(row["Receivables"]) : 0;
                    decimal pay = row["Payables"] != DBNull.Value ? Convert.ToDecimal(row["Payables"]) : 0;

                    totalReceivables += rec;
                    totalPayables += pay;

                    sb.Append($@"
            <tr>
                <td class='text-center'>{sr++}</td>
                <td class='text-center fw-bold'>{hacc}</td>
                <td class='text-left'>{name}</td>
                <td class='text-right'>{(rec > 0 ? rec.ToString("#,##0") : "-")}</td>
                <td class='text-right'>{(pay > 0 ? pay.ToString("#,##0") : "-")}</td>
            </tr>");
                }

                sb.Append($@"
        </tbody>
        <tfoot>
            <tr class='total-row'>
                <td colspan='3' class='text-right fw-bold'>TOTAL:</td>
                <td class='text-right fw-bold'>{totalReceivables:#,##0}</td>
                <td class='text-right fw-bold'>{totalPayables:#,##0}</td>
            </tr>
        </tfoot>
    </table>
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
        public IActionResult ExportExcel(string rcocode, string pac1)
        {
            int companyId = GetCompanyId();
            int financialYearId = GetFinancialYearId();

            try
            {
                DataTable dt = GetReceivablePayableData(rcocode, pac1, companyId, financialYearId);

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

                foreach (DataRow row in dt.Rows)
                {
                    string hacc = EscapeCsv(row["HACC"]?.ToString() ?? "");
                    string name = EscapeCsv(row["Name"]?.ToString() ?? "");
                    decimal rec = row["Receivables"] != DBNull.Value ? Convert.ToDecimal(row["Receivables"]) : 0;
                    decimal pay = row["Payables"] != DBNull.Value ? Convert.ToDecimal(row["Payables"]) : 0;

                    totalRec += rec;
                    totalPay += pay;

                    string recStr = rec > 0 ? rec.ToString("#,##0.00") : "0.00";
                    string payStr = pay > 0 ? pay.ToString("#,##0.00") : "0.00";

                    sb.AppendLine($"{sr++},{hacc},{name},{recStr},{payStr}");
                }

                sb.AppendLine($",,TOTAL:,{totalRec:#,##0.00},{totalPay:#,##0.00}");

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
