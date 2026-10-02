using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Text;
using System;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.Reporting.NETCore;
using Nskg.Models;
using Nskg.Data;
using System.Linq;

namespace Nskg.Controllers
{
    public class VehicleListReportController : Controller
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _context;

        public VehicleListReportController(IConfiguration config, IWebHostEnvironment env, ApplicationDbContext context)
        {
            _config = config;
            _env = env;
            _context = context;
        }

        public IActionResult Index()
        {
            ViewBag.CompanyList = _context.Companies.Where(c => !c.IsDeleted).Select(c => new { c.Id, c.Name, c.Cocode }).ToList();
            ViewBag.SelectedCompanyId = 0;
            return View();
        }

        [HttpGet]
        public IActionResult GetAccountsByCompany(int companyId)
        {
            var query = _context.GLChart3.AsQueryable();
            if (companyId > 0)
            {
                query = query.Where(x => x.CompanyId == companyId || x.CompanyId == 0 || x.CompanyId == null);
            }
            
            var accounts = query
                .Where(x => x.Name != null && (x.Name.Contains("Godown") || x.Name.Contains("Adda") || x.Name.Contains("GODOWN") || x.Name.Contains("ADDA")))
                .Select(x => new
                {
                    code = x.ACC ?? (x.AC1 + x.AC3),
                    name = x.Name,
                    type = (x.Name.ToLower().Contains("adda") ? "Adda" : "Godown")
                })
                .Distinct()
                .OrderBy(x => x.name)
                .ToList();

            return Json(accounts);
        }

        private int GetCompanyId(int? companyId)
        {
            if (companyId.HasValue)
                return companyId.Value;
            return 0;
        }

        private DataTable GetVehicleListData(DateTime? fromDate, DateTime? toDate, string? accode, out string resolvedType, int companyId)
        {
            DataTable dt = new DataTable();
            resolvedType = "Godown"; 

            if (!string.IsNullOrEmpty(accode))
            {
                var ac = _context.GLChart3.FirstOrDefault(x => (x.ACC == accode || (x.AC1 + x.AC3) == accode));
                if (ac != null && ac.Name != null && ac.Name.IndexOf("Adda", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    resolvedType = "Adda";
                }
            }

            string connString = _config.GetConnectionString("DefaultConnection");
            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();
                string query = @"
                    SELECT 
                        ch.DocDate AS VoDate,
                        ch.ChalNo AS BillTiNo,
                        ch.Station AS Station,
                        ISNULL(ch.BillTiAmt,0)+ISNULL(ch.PartyExAmt,0)+ISNULL(ch.Lifter2Amt,0)+ISNULL(ch.OtherExAmt,0)+ISNULL(ch.LocalAmt,0)+ISNULL(ch.Tax,0)+ISNULL(ch.Labour,0) AS Rate,
                        ISNULL(ch.NetAmt,0) AS Amount,
                        ch.VehicleNo AS VehicleNo,
                        ISNULL(ch.TotNet,0) AS DrAmt,
                        ISNULL(ch.LocalAmt,0) AS CrAmt,
                        ISNULL(ch.DeliveryAmt,0) AS DeliveryAmt,
                        ISNULL(ch.PaidAmt,0) AS Ndramt,
                        ISNULL(ch.StationAmt,0) AS Ncramt,
                        ch.StationCode AS Ac1Ac3,
                        ISNULL(ch.DeliveryAmt1,0) AS STaxAmt,
                        ISNULL(ch.DeliveryAmt2,0) AS DeliveryAmt2,
                        ch.Transporter AS IName,
                        c.Cocode AS CoCode,
                        ISNULL(ch.TransporterAmt,0) AS Pay,
                        ISNULL(ch.AdvanceAmt,0) AS Qty,
                        c.Name AS CompanyName
                    FROM CommHead ch
                    LEFT JOIN Companies c ON c.Id = ch.CompanyId
                    WHERE ISNULL(ch.IsDeleted, 0) = 0
                ";

                if (fromDate.HasValue) query += " AND ch.DocDate >= @FromDate ";
                if (toDate.HasValue) query += " AND ch.DocDate <= @ToDate ";
                if (!string.IsNullOrWhiteSpace(accode))
                {
                    query += @" AND (
                        ch.StationCode = @Accode 
                        OR ch.Station = (SELECT TOP 1 Name FROM GLChart3 WHERE ACC = @Accode OR (AC1 + AC3) = @Accode)
                        OR ch.StationId IN (SELECT Id FROM GLChart3 WHERE ACC = @Accode OR (AC1 + AC3) = @Accode)
                    ) ";
                }
                
                if (companyId > 0)
                {
                    query += " AND ch.CompanyId = @CompanyId ";
                }

                query += " ORDER BY ch.DocDate, ch.ChalNo";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    if (fromDate.HasValue) cmd.Parameters.AddWithValue("@FromDate", fromDate.Value);
                    if (toDate.HasValue) cmd.Parameters.AddWithValue("@ToDate", toDate.Value);
                    if (!string.IsNullOrWhiteSpace(accode)) cmd.Parameters.AddWithValue("@Accode", accode);
                    if (companyId > 0) cmd.Parameters.AddWithValue("@CompanyId", companyId);

                    using (SqlDataAdapter sda = new SqlDataAdapter(cmd))
                    {
                        sda.Fill(dt);
                    }
                }
            }
            return dt;
        }

        [HttpGet]
        public IActionResult OnScreenReport(DateTime? fromDate, DateTime? toDate, string? accode, int? companyId, bool isPrintView = false)
        {
            int selectedCompanyId = GetCompanyId(companyId);
            try
            {
                DataTable dt = GetVehicleListData(fromDate, toDate, accode, out string resolvedType, selectedCompanyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No data found for the selected filter criteria.</strong></div>", "text/html");
                }

                string compName = selectedCompanyId == 0 ? "All Companies" : ((dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company");
                string reportTitle = (resolvedType == "Adda") ? "VEHICLE LIST REPORT (ADDA - commreport_all12)" : "VEHICLE LIST REPORT (GODOWN - commreportall_1)";
                
                var sb = new StringBuilder();
                sb.Append(@"<!DOCTYPE html><html><head><meta charset='utf-8'>
                <style>
                    body{font-family:Arial,sans-serif;font-size:11px;margin:10px;color:#333;}
                    .header-box{text-align:center;margin-bottom:12px;}
                    .header-box h2{margin:0 0 4px;color:#0d6efd;font-size:18px;}
                    .header-box h3{margin:0 0 4px;font-size:14px;color:#495057;}
                    table{width:100%;border-collapse:collapse;margin-top:8px;}
                    th{background:#0d6efd;color:#fff;padding:6px 4px;text-align:left;font-size:10px;white-space:nowrap;border:1px solid #0b5ed7;}
                    td{padding:4px;border:1px solid #dee2e6;font-size:10px;white-space:nowrap;}
                    tr:nth-child(even){background:#f8f9fa;}
                    tr:hover td{background:#e7f1ff;}
                    tfoot tr{background:#d0e2ff;font-weight:bold;}
                    .num{text-align:right;}
                    .center{text-align:center;}
                    .bold{font-weight:bold;}
                    @media print {
                        .no-print { display: none; }
                        body { margin: 0; }
                    }
                </style>");

                if (isPrintView)
                {
                    sb.Append("<script>window.onload = function() { window.print(); };</script>");
                }

                sb.Append("</head><body>");

                sb.Append("<div class='header-box'>");
                sb.Append($"<h2>{compName}</h2>");
                sb.Append($"<h3>{reportTitle}</h3>");
                sb.Append($"<p>Date From: <b>{(fromDate.HasValue ? fromDate.Value.ToString("dd-MMM-yyyy") : "")}</b> To: <b>{(toDate.HasValue ? toDate.Value.ToString("dd-MMM-yyyy") : "")}</b></p>");
                sb.Append("</div>");

                sb.Append("<table><thead><tr>");
                if (resolvedType == "Godown")
                {
                    sb.Append("<th class='center'>Chal No</th>");
                    sb.Append("<th>Date</th>");
                    sb.Append("<th>Comp</th>");
                    sb.Append("<th>Station</th>");
                    sb.Append("<th>Transporter</th>");
                    sb.Append("<th>Vehicle No</th>");
                    sb.Append("<th class='num'>Challan AMT</th>");
                    sb.Append("<th class='num'>Munsiana</th>");
                    sb.Append("<th class='num'>Fright</th>");
                    sb.Append("<th class='num'>Delivery Amt</th>");
                    sb.Append("<th class='num'>Delivery 6%</th>");
                    sb.Append("<th class='num'>Station amt</th>");
                    sb.Append("<th class='num'>Advance amt</th>");
                    sb.Append("<th class='num'>Transporter amt</th>");
                    sb.Append("<th class='num'>Paid AMT</th>");
                    sb.Append("<th class='num'>Expense</th>");
                    sb.Append("<th class='num'>Total AMT</th>");
                }
                else
                {
                    sb.Append("<th>Date</th>");
                    sb.Append("<th>Station</th>");
                    sb.Append("<th>Vehicle no</th>");
                    sb.Append("<th class='num'>Fright</th>");
                }
                sb.Append("</tr></thead><tbody>");

                decimal totChalAmt = 0, totMunsiana = 0, totFright = 0, totDeliv = 0, totDeliv6 = 0, totStn = 0, totAdv = 0, totTransp = 0, totPaid = 0, totExp = 0, totAmt = 0;

                foreach (DataRow row in dt.Rows)
                {
                    string voDate = row["VoDate"] != DBNull.Value ? Convert.ToDateTime(row["VoDate"]).ToString("dd/MM/yy") : "";
                    string chalNo = row["BillTiNo"]?.ToString() ?? "";
                    string stn = row["Station"]?.ToString() ?? "";
                    string veh = row["VehicleNo"]?.ToString() ?? "";
                    string cCode = row["CoCode"]?.ToString();
                    string comp = cCode == "01" ? "W.H" : (cCode == "02" ? "M.P" : (cCode == "03" ? "N.K" : (cCode == "04" ? "R.W" : (cCode ?? ""))));
                    string trans = row["IName"]?.ToString() ?? "";

                    decimal rate = row["Rate"] != DBNull.Value ? Convert.ToDecimal(row["Rate"]) : 0;
                    decimal totNet = row["DrAmt"] != DBNull.Value ? Convert.ToDecimal(row["DrAmt"]) : 0;
                    decimal localAmt = row["CrAmt"] != DBNull.Value ? Convert.ToDecimal(row["CrAmt"]) : 0;
                    decimal deliveryAmt = row["DeliveryAmt"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt"]) : 0;
                    decimal sTaxAmt = row["STaxAmt"] != DBNull.Value ? Convert.ToDecimal(row["STaxAmt"]) : 0;
                    decimal stnAmt = row["Ncramt"] != DBNull.Value ? Convert.ToDecimal(row["Ncramt"]) : 0;
                    decimal advAmt = row["Qty"] != DBNull.Value ? Convert.ToDecimal(row["Qty"]) : 0;
                    decimal transpAmt = row["Pay"] != DBNull.Value ? Convert.ToDecimal(row["Pay"]) : 0;
                    decimal paidAmt = row["Ndramt"] != DBNull.Value ? Convert.ToDecimal(row["Ndramt"]) : 0;
                    decimal deliveryAmt2 = row["DeliveryAmt2"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt2"]) : 0;

                    decimal challanAmt = totNet;
                    decimal munsiana = localAmt;
                    decimal fright = deliveryAmt;
                    decimal deliv = sTaxAmt;
                    decimal deliv6 = deliveryAmt2;
                    decimal expAmt = rate;
                    
                    // Formula derived from Oracle Report behaviors for Transporter, Paid, Advance and Expense connection
                    decimal totalAmt = transpAmt + paidAmt + advAmt - expAmt;

                    totChalAmt += challanAmt; totMunsiana += munsiana; totFright += fright; totDeliv += deliv; totDeliv6 += deliv6;
                    totStn += stnAmt; totAdv += advAmt; totTransp += transpAmt; totPaid += paidAmt; totExp += expAmt; totAmt += totalAmt;

                    sb.Append("<tr>");
                    if (resolvedType == "Godown")
                    {
                        sb.Append($"<td class='center'>{chalNo}</td>");
                        sb.Append($"<td>{voDate}</td>");
                        sb.Append($"<td class='bold text-primary'>{comp}</td>");
                        sb.Append($"<td>{stn}</td>");
                        sb.Append($"<td>{trans}</td>");
                        sb.Append($"<td>{veh}</td>");
                        sb.Append($"<td class='num'>{challanAmt:#,##0}</td>");
                        sb.Append($"<td class='num'>{munsiana:#,##0}</td>");
                        sb.Append($"<td class='num'>{fright:#,##0}</td>");
                        sb.Append($"<td class='num'>{deliv:#,##0}</td>");
                        sb.Append($"<td class='num'>{deliv6:#,##0}</td>");
                        sb.Append($"<td class='num'>{stnAmt:#,##0}</td>");
                        sb.Append($"<td class='num'>{advAmt:#,##0}</td>");
                        sb.Append($"<td class='num'>{transpAmt:#,##0}</td>");
                        sb.Append($"<td class='num'>{paidAmt:#,##0}</td>");
                        sb.Append($"<td class='num'>{expAmt:#,##0}</td>");
                        sb.Append($"<td class='num'>{totalAmt:#,##0}</td>");
                    }
                    else
                    {
                        sb.Append($"<td>{voDate}</td>");
                        sb.Append($"<td>{stn}</td>");
                        sb.Append($"<td>{veh}</td>");
                        sb.Append($"<td class='num'>{fright:#,##0}</td>");
                    }
                    sb.Append("</tr>");
                }

                sb.Append("</tbody><tfoot><tr>");
                if (resolvedType == "Godown")
                {
                    sb.Append("<td colspan='6' style='text-align:right;' class='bold'>GRAND TOTAL:</td>");
                    sb.Append($"<td class='num bold'>{totChalAmt:#,##0}</td>");
                    sb.Append($"<td class='num bold'>{totMunsiana:#,##0}</td>");
                    sb.Append($"<td class='num bold'>{totFright:#,##0}</td>");
                    sb.Append($"<td class='num bold'>{totDeliv:#,##0}</td>");
                    sb.Append($"<td class='num bold'>{totDeliv6:#,##0}</td>");
                    sb.Append($"<td class='num bold'>{totStn:#,##0}</td>");
                    sb.Append($"<td class='num bold'>{totAdv:#,##0}</td>");
                    sb.Append($"<td class='num bold'>{totTransp:#,##0}</td>");
                    sb.Append($"<td class='num bold'>{totPaid:#,##0}</td>");
                    sb.Append($"<td class='num bold'>{totExp:#,##0}</td>");
                    sb.Append($"<td class='num bold'>{totAmt:#,##0}</td>");
                }
                else
                {
                    sb.Append("<td colspan='3' style='text-align:right;' class='bold'>GRAND TOTAL:</td>");
                    sb.Append($"<td class='num bold'>{totFright:#,##0}</td>");
                }
                sb.Append("</tr></tfoot></table></body></html>");

                return Content(sb.ToString(), "text/html");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}", "text/html");
            }
        }

        [HttpGet]
        public IActionResult ExportExcel(DateTime? fromDate, DateTime? toDate, string? accode, int? companyId)
        {
            int selectedCompanyId = GetCompanyId(companyId);

            try
            {
                DataTable dt = GetVehicleListData(fromDate, toDate, accode, out string resolvedType, selectedCompanyId);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                var sb = new StringBuilder();
                string compName = selectedCompanyId == 0 ? "All Companies" : ((dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company");
                string reportTitle = (resolvedType == "Adda") ? "VEHICLE LIST REPORT (ADDA - commreport_all12)" : "VEHICLE LIST REPORT (GODOWN - commreportall_1)";
                sb.AppendLine($"\"{compName}\"");
                sb.AppendLine($"\"{reportTitle}\"");
                sb.AppendLine($"\"Date From:\",\"{fromDate:dd-MM-yyyy}\",\"Date To:\",\"{toDate:dd-MM-yyyy}\",\"Account:\",\"{accode}\"");
                sb.AppendLine();

                if (resolvedType == "Godown")
                {
                    sb.AppendLine("Chal no,Date,Comp,Station,Transporter,Vehicle no,Challan AMT,Munsiana,Fright,Delivery Amt,Delivery 6%,Station amt,Advance amt,Transporter amt,Paid AMT,Expense,Total AMT");
                }
                else
                {
                    sb.AppendLine("Date,Station,Vehicle no,Fright");
                }

                foreach (DataRow row in dt.Rows)
                {
                    string voDate = row["VoDate"] != DBNull.Value ? Convert.ToDateTime(row["VoDate"]).ToString("dd/MM/yy") : "";
                    string chalNo = row["BillTiNo"]?.ToString() ?? "";
                    string stn = row["Station"]?.ToString() ?? "";
                    string veh = row["VehicleNo"]?.ToString() ?? "";
                    string cCode = row["CoCode"]?.ToString();
                    string comp = cCode == "01" ? "W.H" : (cCode == "02" ? "M.P" : (cCode == "03" ? "N.K" : (cCode == "04" ? "R.W" : (cCode ?? ""))));
                    string trans = row["IName"]?.ToString() ?? "";

                    decimal rate = row["Rate"] != DBNull.Value ? Convert.ToDecimal(row["Rate"]) : 0;
                    decimal totNet = row["DrAmt"] != DBNull.Value ? Convert.ToDecimal(row["DrAmt"]) : 0;
                    decimal localAmt = row["CrAmt"] != DBNull.Value ? Convert.ToDecimal(row["CrAmt"]) : 0;
                    decimal deliveryAmt = row["DeliveryAmt"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt"]) : 0;
                    decimal sTaxAmt = row["STaxAmt"] != DBNull.Value ? Convert.ToDecimal(row["STaxAmt"]) : 0;
                    decimal stnAmt = row["Ncramt"] != DBNull.Value ? Convert.ToDecimal(row["Ncramt"]) : 0;
                    decimal advAmt = row["Qty"] != DBNull.Value ? Convert.ToDecimal(row["Qty"]) : 0;
                    decimal transpAmt = row["Pay"] != DBNull.Value ? Convert.ToDecimal(row["Pay"]) : 0;
                    decimal paidAmt = row["Ndramt"] != DBNull.Value ? Convert.ToDecimal(row["Ndramt"]) : 0;
                    decimal deliveryAmt2 = row["DeliveryAmt2"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt2"]) : 0;

                    decimal challanAmt = totNet;
                    decimal munsiana = localAmt;
                    decimal fright = deliveryAmt;
                    decimal deliv = sTaxAmt;
                    decimal deliv6 = deliveryAmt2;
                    decimal expAmt = rate;
                    
                    decimal totalAmt = transpAmt + paidAmt + advAmt - expAmt;

                    if (resolvedType == "Godown")
                    {
                        sb.AppendLine($"{chalNo},{voDate},{comp},{EscapeCsv(stn)},{EscapeCsv(trans)},{EscapeCsv(veh)},{challanAmt},{munsiana},{fright},{deliv},{deliv6},{stnAmt},{advAmt},{transpAmt},{paidAmt},{expAmt},{totalAmt}");
                    }
                    else
                    {
                        sb.AppendLine($"{voDate},{EscapeCsv(stn)},{EscapeCsv(veh)},{fright}");
                    }
                }

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"VehicleList_{resolvedType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult GeneratePDF(DateTime? fromDate, DateTime? toDate, string? accode, int? companyId)
        {
            return OnScreenReport(fromDate, toDate, accode, companyId, isPrintView: true);
        }

        private string EscapeCsv(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Contains(",") || text.Contains("\"") || text.Contains("\n") || text.Contains("\r"))
            {
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }
            return text;
        }
    }
}
