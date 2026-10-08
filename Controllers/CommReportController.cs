using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Reporting.NETCore;
using Nskg.Data;
using Nskg.Extensions;
using System;
using System.Data;
using System.IO;
using System.Text;

namespace Nskg.Controllers
{
    [Authorize]
    public class CommReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public CommReportController(ApplicationDbContext context, IConfiguration config, IWebHostEnvironment env)
        {
            _context = context;
            _config = config;
            _env = env;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private int GetCompanyId(int? companyId = null, string? pcocode = null)
        {
            if (companyId.HasValue) return companyId.Value;
            if (!string.IsNullOrWhiteSpace(pcocode))
            {
                if (pcocode.Trim() == "0" || pcocode.Trim().Equals("All", StringComparison.OrdinalIgnoreCase)) return 0;
                var comp = _context.Companies.FirstOrDefault(c => c.Cocode == pcocode.Trim() && !c.IsDeleted);
                if (comp != null) return comp.Id;
            }
            return 0;
        }

        private int GetFinancialYearId()
        {
            var fyId = User.FindFirst("FinancialYearId")?.Value;
            if (int.TryParse(fyId, out int id) && id > 0) return id;
            return 4;
        }

        [HttpGet]
        public IActionResult Index(string? fromDate, string? toDate, string? partyCode, string? reportType, decimal? biltyNo, string? vehicleNo, string? station, int? companyId, string? pchalno, string? pcocode, string? fdate, string? tdate, string? mode)
        {
            int selectedCompanyId = GetCompanyId(companyId, pcocode);

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

            string effectiveFromDate = !string.IsNullOrEmpty(fdate) ? fdate : fromDate;
            string effectiveToDate = !string.IsNullOrEmpty(tdate) ? tdate : toDate;

            ViewBag.CompanyList = companies;
            ViewBag.SelectedCompanyId = selectedCompanyId;
            ViewBag.Pcocode = pcocode ?? "";
            ViewBag.Pchalno = pchalno ?? "";
            ViewBag.FromDate = string.IsNullOrEmpty(effectiveFromDate) ? DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd") : effectiveFromDate;
            ViewBag.ToDate = string.IsNullOrEmpty(effectiveToDate) ? DateTime.Now.ToString("yyyy-MM-dd") : effectiveToDate;
            ViewBag.PartyCode = partyCode ?? "";
            ViewBag.ReportType = string.IsNullOrEmpty(reportType) ? "Godown" : reportType;
            ViewBag.BiltyNo = biltyNo;
            ViewBag.VehicleNo = vehicleNo;
            ViewBag.Station = station;
            ViewBag.Mode = string.IsNullOrEmpty(mode) ? (string.IsNullOrEmpty(pchalno) ? "Report" : "List") : mode;

            return View("Index");
        }

        [HttpGet]
        public IActionResult ChallanList(string? pchalno, string? pcocode, int? companyId)
        {
            return Index(null, null, null, "Godown", null, null, null, companyId, pchalno, pcocode, null, null, "List");
        }

        [HttpGet]
        public IActionResult ChallanReport(string? fdate, string? tdate, string? pcocode, string? fromDate, string? toDate, int? companyId)
        {
            return Index(fromDate, toDate, null, "Adda", null, null, null, companyId, null, pcocode, fdate, tdate, "Report");
        }

        /// <summary>
        /// Challan List Data Retrieval from ChallanHead
        /// </summary>
        private DataTable GetChallanListData(string? pchalno, int companyId)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                string companyName = "West Wharf-New Shadab Karachi Goods Transports";
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
                else
                {
                    companyName = "All Companies";
                }

                string query = @"
                    SELECT
                        h.DocNo,
                        h.DocDate,
                        h.ChalNo,
                        COALESCE(NULLIF(RTRIM(h.Station), ''), (SELECT TOP 1 g.Name FROM GLChart3 g WHERE g.Id = h.StationId), '') AS Station,
                        ISNULL(h.VehicleNo, '') AS Vehicle,
                        ISNULL(h.Driver, '') AS Driver,
                        COALESCE(NULLIF(RTRIM(h.Transporter), ''), (SELECT TOP 1 g.Name FROM GLChart3 g WHERE g.Id = h.TransId), '') AS Transporter,
                        CASE
                            WHEN ISNULL(h.NetAmt, 0) <> 0 THEN h.NetAmt
                            WHEN ISNULL(h.TotPaid, 0) + ISNULL(h.TotToPaid, 0) <> 0 THEN ISNULL(h.TotPaid, 0) + ISNULL(h.TotToPaid, 0)
                            WHEN ISNULL(h.TotBillTi, 0) <> 0 THEN h.TotBillTi
                            WHEN ISNULL(h.BillTiAmt, 0) <> 0 THEN h.BillTiAmt
                            WHEN ISNULL(h.PExpAmt, 0) <> 0 THEN h.PExpAmt
                            ELSE ISNULL((SELECT SUM(ISNULL(cd.PaidAmt, 0) + ISNULL(cd.ToPaidAmt, 0) + ISNULL(cd.BillTiAmt, 0) + ISNULL(cd.MAmount, 0)) FROM ChallanDet cd WHERE cd.ChallanHeadId = h.Id), 0)
                        END AS TotAmt,
                        CASE c.Cocode
                            WHEN '01' THEN 'W.H'
                            WHEN '02' THEN 'M.P'
                            WHEN '03' THEN 'N.K'
                            WHEN '04' THEN 'R.W'
                            ELSE ISNULL(c.Cocode, '')
                        END AS Branch,
                        ISNULL(h.Narration, '') AS Narration,
                        @CompanyName AS CompanyName
                    FROM ChallanHead h
                    LEFT JOIN Companies c ON c.Id = h.CompanyId
                    WHERE ISNULL(h.IsDeleted, 0) = 0
                      AND (@CompanyId = 0 OR h.CompanyId = @CompanyId OR h.CoCode = (SELECT TOP 1 Cocode FROM Companies WHERE Id = @CompanyId))
                      AND (
                          @Pchalno = ''
                          OR (ISNUMERIC(@Pchalno) = 1 AND h.ChalNo = CAST(@Pchalno AS INT))
                          OR h.DocNo = @Pchalno
                      )
                    ORDER BY h.DocDate ASC, h.DocNo ASC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    cmd.Parameters.AddWithValue("@CompanyName", companyName);
                    cmd.Parameters.AddWithValue("@Pchalno", string.IsNullOrWhiteSpace(pchalno) ? "" : pchalno.Trim());

                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }

            return dt;
        }

        /// <summary>
        /// Challan Report Data (from ChallanHead + ChallanDet) — columns matching sample PDF:
        /// Date | No | Station | Vehicle | Transporter | Delivery Amt | Lifter Amt | Billti Amt | To Paid | Party Exp | Local Amt | Other Exp | Net Amt
        /// </summary>
        private DataTable GetChallanReportData(DateTime? fromDate, DateTime? toDate, int companyId, string? pchalno = null)
        {
            DataTable dt = new DataTable();
            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                con.Open();

                string companyName = "All Companies";
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

                string query = @"
                    SELECT
                        h.DocDate                                                                   AS DocDate,
                        ISNULL(CAST(h.ChalNo AS VARCHAR(30)), '')                                  AS No,
                        COALESCE(NULLIF(RTRIM(h.Station), ''),
                            (SELECT TOP 1 g.Name FROM GLChart3 g WHERE g.Id = h.StationId), '')    AS Station,
                        ISNULL(h.VehicleNo, '')                                                    AS Vehicle,
                        COALESCE(NULLIF(RTRIM(h.Transporter), ''),
                            (SELECT TOP 1 g2.Name FROM GLChart3 g2 WHERE g2.Id = h.TransId), '')   AS Transporter,
                        h.DeliveryAmt                                                              AS DeliveryAmt,
                        h.TotLifter2                                                               AS LifterAmt,
                        h.BillTiAmt                                                                AS BilltiAmt,
                        h.TotToPaid                                                                AS ToPaid,
                        ISNULL(h.PartyEx2, '')                                                     AS BParty,
                        COALESCE(h.TotPartyEx, ISNULL(h.PExpAmt, 0) + ISNULL(h.PExpAmt2, 0) + ISNULL(h.PExpAmt3, 0), 0) AS PartyExp,
                        ISNULL(h.LocalAmt2, '')                                                    AS BLocal,
                        h.LocalAmt                                                                 AS LocalAmt,
                        ISNULL(h.OtherEx2, '')                                                     AS BOtherEx,
                        h.TotOtherEx                                                               AS OtherExp,
                        h.NetAmt                                                                   AS NetAmt,
                        CASE c.Cocode
                            WHEN '01' THEN 'W.H'
                            WHEN '02' THEN 'M.P'
                            WHEN '03' THEN 'N.K'
                            WHEN '04' THEN 'R.W'
                            ELSE ISNULL(c.Cocode, '')
                        END                                                                        AS Branch,
                        ISNULL(h.ChalNo, 0)                                                        AS ChalNo,
                        @CompanyName                                                               AS CompanyName,
                        @FromDate                                                                  AS FromDate,
                        @ToDate                                                                    AS ToDate
                    FROM ChallanHead h
                    LEFT JOIN Companies c ON c.Id = h.CompanyId
                    WHERE ISNULL(h.IsDeleted, 0) = 0
                      AND (@CompanyId = 0 OR h.CompanyId = @CompanyId)";

                if (!string.IsNullOrWhiteSpace(pchalno))
                    query += " AND (CAST(ISNULL(h.ChalNo, 0) AS VARCHAR(50)) = @Pchalno OR h.DocNo LIKE '%' + @Pchalno + '%')";

                if (fromDate.HasValue)
                    query += " AND h.DocDate >= @FromDate";

                if (toDate.HasValue)
                    query += " AND h.DocDate <= @ToDate";

                query += " ORDER BY h.DocDate ASC, h.ChalNo ASC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    cmd.Parameters.AddWithValue("@CompanyName", companyName);
                    cmd.Parameters.AddWithValue("@Pchalno", string.IsNullOrWhiteSpace(pchalno) ? (object)DBNull.Value : pchalno.Trim());

                    if (fromDate.HasValue)
                        cmd.Parameters.AddWithValue("@FromDate", fromDate.Value.Date);
                    else
                        cmd.Parameters.AddWithValue("@FromDate", DBNull.Value);

                    if (toDate.HasValue)
                        cmd.Parameters.AddWithValue("@ToDate", toDate.Value.Date);
                    else
                        cmd.Parameters.AddWithValue("@ToDate", DBNull.Value);

                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult GeneratePDF(DateTime? fromDate, DateTime? toDate, string? partyCode, string? reportType, decimal? biltyNo, string? vehicleNo, string? station, int? companyId, string? pchalno, string? pcocode, string? fdate, string? tdate, string? mode = null)
        {
            int selectedCompanyId = GetCompanyId(companyId, pcocode);
            bool isListMode = string.Equals(mode, "List", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(pchalno);

            if (isListMode)
            {
                // Printable HTML for Challan List
                return OnScreenReport(fromDate, toDate, partyCode, reportType, biltyNo, vehicleNo, station, companyId, pchalno, pcocode, fdate, tdate, mode, isPrintView: true);
            }

            // Challan Report — generate printable HTML from ChallanHead+ChallanDet
            DateTime? effFromDate = !string.IsNullOrEmpty(fdate) && DateTime.TryParse(fdate, out DateTime fd) ? fd : fromDate;
            DateTime? effToDate = !string.IsNullOrEmpty(tdate) && DateTime.TryParse(tdate, out DateTime td) ? td : toDate;

            try
            {
                DataTable dt = GetChallanReportData(effFromDate, effToDate, selectedCompanyId, pchalno);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found for the selected filter criteria.");
                }

                string compName = dt.Rows[0]["CompanyName"]?.ToString() ?? "Company";
                string periodText = (effFromDate.HasValue && effToDate.HasValue)
                    ? $"{effFromDate.Value:dd-MMM-yyyy} to {effToDate.Value:dd-MMM-yyyy}"
                    : "All Dates";

                decimal totDelivery = 0, totLifter = 0, totBillti = 0, totToPaid = 0;
                decimal totPartyExp = 0, totLocalAmt = 0, totOtherExp = 0, totNet = 0;

                var sbPdf = new StringBuilder();
                sbPdf.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
                sbPdf.Append("<title>Challan report</title>");
                sbPdf.Append("<style>");
                sbPdf.Append("body{font-family:Arial,sans-serif;font-size:10px;margin:10px;color:#000;}");
                sbPdf.Append(".title{text-align:center;font-size:16px;font-weight:bold;margin-bottom:8px;}");
                sbPdf.Append(".subtitle{text-align:center;font-size:10px;margin-bottom:8px;color:#333;}");
                sbPdf.Append("table{width:100%;border-collapse:collapse;border:1px solid #000;}");
                sbPdf.Append("th{background:#fff;color:#000;padding:4px 3px;text-align:center;font-size:9.5px;font-weight:bold;border:1px solid #000;white-space:nowrap;}");
                sbPdf.Append("td{padding:3px 4px;border:1px solid #000;font-size:9.5px;white-space:nowrap;}");
                sbPdf.Append(".num{text-align:right;}");
                sbPdf.Append(".center{text-align:center;}");
                sbPdf.Append(".bold{font-weight:bold;}");
                sbPdf.Append("@media print{@page{size:A4 landscape;margin:6mm;}.no-print{display:none;}}");
                sbPdf.Append("</style>");
                sbPdf.Append("<script>window.onload=function(){document.title='Challan report';window.print();};</script>");
                sbPdf.Append("</head><body>");

                sbPdf.Append("<div class='title'>Challan report</div>");
                if (!string.IsNullOrEmpty(compName) && compName != "All Companies")
                {
                    sbPdf.Append($"<div class='subtitle'>{System.Net.WebUtility.HtmlEncode(compName)} &nbsp;|&nbsp; Period: {periodText}</div>");
                }

                sbPdf.Append("<table><thead><tr>");
                sbPdf.Append("<th>Date</th><th>No</th><th>Station</th><th>Vehicle</th><th>Transporter</th>");
                sbPdf.Append("<th class='num'>Delivery Amt</th><th class='num'>Lifter Amt</th><th class='num'>Billti Amt</th>");
                sbPdf.Append("<th class='num'>To paid</th><th>B#/ Party</th><th class='num'>Party Exp</th>");
                sbPdf.Append("<th>B# Local</th><th class='num'>Local Amt</th><th>B#/ Ex Other</th>");
                sbPdf.Append("<th class='num'>Other Exp</th><th class='num'>Net Amt</th>");
                sbPdf.Append("</tr></thead><tbody>");

                foreach (DataRow row in dt.Rows)
                {
                    string docDate = row["DocDate"] != DBNull.Value ? Convert.ToDateTime(row["DocDate"]).ToString("dd/MM/yy") : "";
                    string no = row["No"]?.ToString() ?? "";
                    string stationVal2 = row["Station"]?.ToString() ?? "";
                    string vehicle2 = row["Vehicle"]?.ToString() ?? "";
                    string transporter2 = row["Transporter"]?.ToString() ?? "";

                    decimal? deliveryAmt = row["DeliveryAmt"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt"]) : null;
                    decimal? lifterAmt = row["LifterAmt"] != DBNull.Value ? Convert.ToDecimal(row["LifterAmt"]) : null;
                    decimal? billtiAmt = row["BilltiAmt"] != DBNull.Value ? Convert.ToDecimal(row["BilltiAmt"]) : null;
                    decimal? toPaid = row["ToPaid"] != DBNull.Value ? Convert.ToDecimal(row["ToPaid"]) : null;
                    string bParty = row["BParty"]?.ToString() ?? "";
                    decimal? partyExp = row["PartyExp"] != DBNull.Value ? Convert.ToDecimal(row["PartyExp"]) : null;
                    string bLocal = row["BLocal"]?.ToString() ?? "";
                    decimal? localAmt = row["LocalAmt"] != DBNull.Value ? Convert.ToDecimal(row["LocalAmt"]) : null;
                    string bOtherEx = row["BOtherEx"]?.ToString() ?? "";
                    decimal? otherExp = row["OtherExp"] != DBNull.Value ? Convert.ToDecimal(row["OtherExp"]) : null;
                    decimal? netAmt = row["NetAmt"] != DBNull.Value ? Convert.ToDecimal(row["NetAmt"]) : null;

                    if (deliveryAmt.HasValue) totDelivery += deliveryAmt.Value;
                    if (lifterAmt.HasValue) totLifter += lifterAmt.Value;
                    if (billtiAmt.HasValue) totBillti += billtiAmt.Value;
                    if (toPaid.HasValue) totToPaid += toPaid.Value;
                    if (partyExp.HasValue) totPartyExp += partyExp.Value;
                    if (localAmt.HasValue) totLocalAmt += localAmt.Value;
                    if (otherExp.HasValue) totOtherExp += otherExp.Value;
                    if (netAmt.HasValue) totNet += netAmt.Value;

                    sbPdf.Append("<tr>");
                    sbPdf.Append($"<td class='center'>{docDate}</td>");
                    sbPdf.Append($"<td class='center bold'>{System.Net.WebUtility.HtmlEncode(no)}</td>");
                    sbPdf.Append($"<td>{System.Net.WebUtility.HtmlEncode(stationVal2)}</td>");
                    sbPdf.Append($"<td>{System.Net.WebUtility.HtmlEncode(vehicle2)}</td>");
                    sbPdf.Append($"<td>{System.Net.WebUtility.HtmlEncode(transporter2)}</td>");
                    sbPdf.Append($"<td class='num'>{(deliveryAmt.HasValue ? deliveryAmt.Value.ToString("#,##0") : "")}</td>");
                    sbPdf.Append($"<td class='num'>{(lifterAmt.HasValue ? lifterAmt.Value.ToString("#,##0") : "")}</td>");
                    sbPdf.Append($"<td class='num'>{(billtiAmt.HasValue ? billtiAmt.Value.ToString("#,##0") : "")}</td>");
                    sbPdf.Append($"<td class='num'>{(toPaid.HasValue ? toPaid.Value.ToString("#,##0") : "")}</td>");
                    sbPdf.Append($"<td class='center'>{System.Net.WebUtility.HtmlEncode(bParty)}</td>");
                    sbPdf.Append($"<td class='num'>{(partyExp.HasValue ? partyExp.Value.ToString("#,##0") : "0")}</td>");
                    sbPdf.Append($"<td class='center'>{System.Net.WebUtility.HtmlEncode(bLocal)}</td>");
                    sbPdf.Append($"<td class='num'>{(localAmt.HasValue && localAmt.Value != 0 ? localAmt.Value.ToString("#,##0") : "")}</td>");
                    sbPdf.Append($"<td class='center'>{System.Net.WebUtility.HtmlEncode(bOtherEx)}</td>");
                    sbPdf.Append($"<td class='num'>{(otherExp.HasValue && otherExp.Value != 0 ? otherExp.Value.ToString("#,##0") : "")}</td>");
                    sbPdf.Append($"<td class='num'>{(netAmt.HasValue ? netAmt.Value.ToString("#,##0") : "0")}</td>");
                    sbPdf.Append("</tr>");
                }

                sbPdf.Append("<tr style='background:#f2f2f2;font-weight:bold;'>");
                sbPdf.Append("<td colspan='5' style='text-align:right;'>Total:</td>");
                sbPdf.Append($"<td class='num'>{totDelivery:#,##0}</td>");
                sbPdf.Append($"<td class='num'>{(totLifter != 0 ? totLifter.ToString("#,##0") : "")}</td>");
                sbPdf.Append($"<td class='num'>{totBillti:#,##0}</td>");
                sbPdf.Append($"<td class='num'>{totToPaid:#,##0}</td>");
                sbPdf.Append("<td></td>");
                sbPdf.Append($"<td class='num'>{totPartyExp:#,##0}</td>");
                sbPdf.Append("<td></td>");
                sbPdf.Append($"<td class='num'>{(totLocalAmt != 0 ? totLocalAmt.ToString("#,##0") : "")}</td>");
                sbPdf.Append("<td></td>");
                sbPdf.Append($"<td class='num'>{(totOtherExp != 0 ? totOtherExp.ToString("#,##0") : "")}</td>");
                sbPdf.Append($"<td class='num'>{totNet:#,##0}</td>");
                sbPdf.Append("</tr>");
                sbPdf.Append("</tbody></table></body></html>");

                return Content(sbPdf.ToString(), "text/html");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
            }
        }

        [HttpGet]
        public IActionResult OnScreenReport(DateTime? fromDate, DateTime? toDate, string? partyCode, string? reportType, decimal? biltyNo, string? vehicleNo, string? station, int? companyId, string? pchalno, string? pcocode, string? fdate, string? tdate, string? mode = null, bool isPrintView = false)
        {
            int selectedCompanyId = GetCompanyId(companyId, pcocode);
            bool isListMode = string.Equals(mode, "List", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(pchalno);

            if (isListMode)
            {
                try
                {
                    DataTable dt = GetChallanListData(pchalno, selectedCompanyId);

                    if (dt == null || dt.Rows.Count == 0)
                    {
                        return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No challans found for the selected filter criteria.</strong></div>", "text/html");
                    }

                    string compName = dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() ?? "Company" : "Company";
                    decimal grandTotAmt = 0;

                    var sbList = new StringBuilder();
                    sbList.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
                    sbList.Append("<style>");
                    sbList.Append("body{font-family:Arial,sans-serif;font-size:12px;margin:15px;color:#222;}");
                    sbList.Append(".title-container{text-align:center;margin:10px 0 15px 0;}");
                    sbList.Append(".title-badge{display:inline-block;background:#e9ecef;border:1px solid #adb5bd;padding:6px 50px;border-radius:12px;font-weight:bold;font-size:18px;color:#222;letter-spacing:1px;}");
                    sbList.Append(".meta-info{text-align:center;font-size:12px;color:#666;margin-bottom:12px;}");
                    sbList.Append("table{width:100%;border-collapse:collapse;margin-top:5px;}");
                    sbList.Append("th{background:#f8f9fa;color:#111;padding:8px 6px;text-align:left;font-size:11px;font-weight:bold;border:1px solid #ced4da;white-space:nowrap;}");
                    sbList.Append("td{padding:6px 6px;border:1px solid #dee2e6;font-size:11px;}");
                    sbList.Append("tr:nth-child(even){background:#fafafa;}");
                    sbList.Append("tr:hover td{background:#f1f8ff;}");
                    sbList.Append("tfoot tr{background:#e9ecef;font-weight:bold;}");
                    sbList.Append(".num{text-align:right;}.center{text-align:center;}.bold{font-weight:bold;}");
                    sbList.Append("@media print{.no-print{display:none;}body{margin:0;}}");
                    sbList.Append("</style>");

                    if (isPrintView)
                    {
                        sbList.Append("<script>window.onload = function() { window.print(); };</script>");
                    }

                    sbList.Append("</head><body>");
                    sbList.Append("<div class='title-container'><span class='title-badge'>Challans</span></div>");

                    string pchalText = !string.IsNullOrWhiteSpace(pchalno) ? $" &nbsp;|&nbsp; Chal#: <b>{pchalno}</b>" : "";
                    sbList.Append($"<div class='meta-info'><b>{compName}</b>{pchalText} &nbsp;|&nbsp; Total Records: <b>{dt.Rows.Count}</b></div>");

                    sbList.Append("<table><thead><tr>");
                    sbList.Append("<th style='width:115px;'>Docno</th>");
                    sbList.Append("<th style='width:85px;'>Docdate</th>");
                    sbList.Append("<th style='width:65px;' class='center'>Chal#</th>");
                    sbList.Append("<th style='width:55px;' class='center'>Branch</th>");
                    sbList.Append("<th>Station</th>");
                    sbList.Append("<th style='width:105px;'>Vehicle</th>");
                    sbList.Append("<th style='width:95px;'>Driver</th>");
                    sbList.Append("<th>Transporter</th>");
                    sbList.Append("<th class='num' style='width:105px;'>Tot AMT</th>");
                    sbList.Append("<th>Narration</th>");
                    sbList.Append("</tr></thead><tbody>");

                    foreach (DataRow row in dt.Rows)
                    {
                        decimal totAmt = row["TotAmt"] != DBNull.Value ? Convert.ToDecimal(row["TotAmt"]) : 0;
                        grandTotAmt += totAmt;

                        string docDate = row["DocDate"] != DBNull.Value ? Convert.ToDateTime(row["DocDate"]).ToString("dd/MM/yy") : "";
                        string chalNo = row["ChalNo"] != DBNull.Value ? row["ChalNo"].ToString() : "";
                        string totAmtStr = totAmt != 0 ? totAmt.ToString("#,##0") : "0";

                        sbList.Append("<tr>");
                        sbList.Append($"<td>{row["DocNo"]}</td>");
                        sbList.Append($"<td>{docDate}</td>");
                        sbList.Append($"<td class='center bold'>{chalNo}</td>");
                        sbList.Append($"<td class='center bold' style='color:#0d6efd;'>{row["Branch"]}</td>");
                        sbList.Append($"<td>{row["Station"]}</td>");
                        sbList.Append($"<td class='bold'>{row["Vehicle"]}</td>");
                        sbList.Append($"<td>{row["Driver"]}</td>");
                        sbList.Append($"<td>{row["Transporter"]}</td>");
                        sbList.Append($"<td class='num bold'>{totAmtStr}</td>");
                        sbList.Append($"<td>{row["Narration"]}</td>");
                        sbList.Append("</tr>");
                    }

                    sbList.Append("</tbody><tfoot><tr>");
                    sbList.Append("<td colspan='8' style='text-align:right;' class='bold'>TOTAL:</td>");
                    sbList.Append($"<td class='num bold'>{grandTotAmt:#,##0}</td>");
                    sbList.Append("<td></td>");
                    sbList.Append("</tr></tfoot></table></body></html>");

                    return Content(sbList.ToString(), "text/html");
                }
                catch (Exception ex)
                {
                    return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}", "text/html");
                }
            }

            // =====================================================================
            // Challan Report mode — uses ChallanHead + ChallanDet with correct columns
            // matching sample PDF: Date, No, Station, Vehicle, Transporter,
            // Delivery Amt, Lifter Amt, Billti Amt, To Paid, Party Exp,
            // Local Amt, Other Exp, Net Amt
            // =====================================================================
            DateTime? effFromDate = !string.IsNullOrEmpty(fdate) && DateTime.TryParse(fdate, out DateTime fd) ? fd : fromDate;
            DateTime? effToDate = !string.IsNullOrEmpty(tdate) && DateTime.TryParse(tdate, out DateTime td) ? td : toDate;

            try
            {
                DataTable dt = GetChallanReportData(effFromDate, effToDate, selectedCompanyId, pchalno);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("<div style='font-family:Arial; padding:30px; text-align:center; color:#721c24; background-color:#f8d7da; border:1px solid #f5c6cb; border-radius:6px; margin:20px;'><strong>No data found for the selected filter criteria.</strong></div>", "text/html");
                }

                string compName = dt.Rows[0]["CompanyName"]?.ToString() ?? "Company";
                string periodText = (effFromDate.HasValue && effToDate.HasValue)
                    ? $"Period: <b>{effFromDate.Value:dd-MMM-yyyy}</b> to <b>{effToDate.Value:dd-MMM-yyyy}</b>"
                    : "All Dates";

                decimal grandDelivery = 0, grandLifter = 0, grandBillti = 0, grandToPaid = 0;
                decimal grandPartyExp = 0, grandLocalAmt = 0, grandOtherExp = 0, grandNet = 0;

                var sb = new StringBuilder();
                sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
                sb.Append("<title>Challan report</title>");
                sb.Append("<style>");
                sb.Append("body{font-family:Arial,sans-serif;font-size:11px;margin:15px;color:#000;}");
                sb.Append(".header-box{text-align:center;margin-bottom:12px;}");
                sb.Append(".header-box h2{margin:0 0 4px;font-size:18px;font-weight:bold;color:#111;}");
                sb.Append(".header-box p{margin:0;font-size:11px;color:#555;}");
                sb.Append("table{width:100%;border-collapse:collapse;border:1px solid #000;margin-top:8px;}");
                sb.Append("th{background:#f8f9fa;color:#000;padding:5px 4px;text-align:center;font-size:10px;font-weight:bold;white-space:nowrap;border:1px solid #000;}");
                sb.Append("td{padding:4px 4px;border:1px solid #000;font-size:10.5px;white-space:nowrap;}");
                sb.Append("tr:nth-child(even){background:#fafafa;}");
                sb.Append("tr:hover td{background:#f1f8ff;}");
                sb.Append("tfoot tr{background:#f0f0f0;font-weight:bold;}");
                sb.Append(".num{text-align:right;}.center{text-align:center;}.bold{font-weight:bold;}");
                sb.Append("@media print{.no-print{display:none;}body{margin:5mm;}th,td{border:1px solid #000;}}");
                sb.Append("</style></head><body>");

                sb.Append("<div class='header-box'>");
                sb.Append("<h2>Challan report</h2>");
                if (!string.IsNullOrEmpty(compName) && compName != "All Companies")
                {
                    sb.Append($"<p>{System.Net.WebUtility.HtmlEncode(compName)} &nbsp;|&nbsp; {periodText}</p>");
                }
                else
                {
                    sb.Append($"<p>{periodText}</p>");
                }
                sb.Append("</div>");

                sb.Append("<table><thead><tr>");
                sb.Append("<th>Date</th><th>No</th><th>Station</th><th>Vehicle</th><th>Transporter</th>");
                sb.Append("<th class='num'>Delivery Amt</th><th class='num'>Lifter Amt</th><th class='num'>Billti Amt</th>");
                sb.Append("<th class='num'>To paid</th><th>B#/ Party</th><th class='num'>Party Exp</th>");
                sb.Append("<th>B# Local</th><th class='num'>Local Amt</th><th>B#/ Ex Other</th>");
                sb.Append("<th class='num'>Other Exp</th><th class='num'>Net Amt</th>");
                sb.Append("</tr></thead><tbody>");

                foreach (DataRow row in dt.Rows)
                {
                    string docDate = row["DocDate"] != DBNull.Value ? Convert.ToDateTime(row["DocDate"]).ToString("dd/MM/yy") : "";
                    string no = row["No"]?.ToString() ?? "";
                    string stationVal = row["Station"]?.ToString() ?? "";
                    string vehicle = row["Vehicle"]?.ToString() ?? "";
                    string transporter = row["Transporter"]?.ToString() ?? "";

                    decimal? deliveryAmt = row["DeliveryAmt"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt"]) : null;
                    decimal? lifterAmt = row["LifterAmt"] != DBNull.Value ? Convert.ToDecimal(row["LifterAmt"]) : null;
                    decimal? billtiAmt = row["BilltiAmt"] != DBNull.Value ? Convert.ToDecimal(row["BilltiAmt"]) : null;
                    decimal? toPaid = row["ToPaid"] != DBNull.Value ? Convert.ToDecimal(row["ToPaid"]) : null;
                    string bParty = row["BParty"]?.ToString() ?? "";
                    decimal? partyExp = row["PartyExp"] != DBNull.Value ? Convert.ToDecimal(row["PartyExp"]) : null;
                    string bLocal = row["BLocal"]?.ToString() ?? "";
                    decimal? localAmt = row["LocalAmt"] != DBNull.Value ? Convert.ToDecimal(row["LocalAmt"]) : null;
                    string bOtherEx = row["BOtherEx"]?.ToString() ?? "";
                    decimal? otherExp = row["OtherExp"] != DBNull.Value ? Convert.ToDecimal(row["OtherExp"]) : null;
                    decimal? netAmt = row["NetAmt"] != DBNull.Value ? Convert.ToDecimal(row["NetAmt"]) : null;

                    if (deliveryAmt.HasValue) grandDelivery += deliveryAmt.Value;
                    if (lifterAmt.HasValue) grandLifter += lifterAmt.Value;
                    if (billtiAmt.HasValue) grandBillti += billtiAmt.Value;
                    if (toPaid.HasValue) grandToPaid += toPaid.Value;
                    if (partyExp.HasValue) grandPartyExp += partyExp.Value;
                    if (localAmt.HasValue) grandLocalAmt += localAmt.Value;
                    if (otherExp.HasValue) grandOtherExp += otherExp.Value;
                    if (netAmt.HasValue) grandNet += netAmt.Value;

                    sb.Append("<tr>");
                    sb.Append($"<td class='center'>{docDate}</td>");
                    sb.Append($"<td class='center bold'>{System.Net.WebUtility.HtmlEncode(no)}</td>");
                    sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(stationVal)}</td>");
                    sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(vehicle)}</td>");
                    sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(transporter)}</td>");
                    sb.Append($"<td class='num'>{(deliveryAmt.HasValue ? deliveryAmt.Value.ToString("#,##0") : "")}</td>");
                    sb.Append($"<td class='num'>{(lifterAmt.HasValue ? lifterAmt.Value.ToString("#,##0") : "")}</td>");
                    sb.Append($"<td class='num'>{(billtiAmt.HasValue ? billtiAmt.Value.ToString("#,##0") : "")}</td>");
                    sb.Append($"<td class='num'>{(toPaid.HasValue ? toPaid.Value.ToString("#,##0") : "")}</td>");
                    sb.Append($"<td class='center'>{System.Net.WebUtility.HtmlEncode(bParty)}</td>");
                    sb.Append($"<td class='num'>{(partyExp.HasValue ? partyExp.Value.ToString("#,##0") : "0")}</td>");
                    sb.Append($"<td class='center'>{System.Net.WebUtility.HtmlEncode(bLocal)}</td>");
                    sb.Append($"<td class='num'>{(localAmt.HasValue && localAmt.Value != 0 ? localAmt.Value.ToString("#,##0") : "")}</td>");
                    sb.Append($"<td class='center'>{System.Net.WebUtility.HtmlEncode(bOtherEx)}</td>");
                    sb.Append($"<td class='num'>{(otherExp.HasValue && otherExp.Value != 0 ? otherExp.Value.ToString("#,##0") : "")}</td>");
                    sb.Append($"<td class='num'>{(netAmt.HasValue ? netAmt.Value.ToString("#,##0") : "0")}</td>");
                    sb.Append("</tr>");
                }

                sb.Append("</tbody><tfoot><tr>");
                sb.Append("<td colspan='5' style='text-align:right;' class='bold'>Total:</td>");
                sb.Append($"<td class='num bold'>{grandDelivery:#,##0}</td>");
                sb.Append($"<td class='num bold'>{(grandLifter != 0 ? grandLifter.ToString("#,##0") : "")}</td>");
                sb.Append($"<td class='num bold'>{grandBillti:#,##0}</td>");
                sb.Append($"<td class='num bold'>{grandToPaid:#,##0}</td>");
                sb.Append("<td></td>");
                sb.Append($"<td class='num bold'>{grandPartyExp:#,##0}</td>");
                sb.Append("<td></td>");
                sb.Append($"<td class='num bold'>{(grandLocalAmt != 0 ? grandLocalAmt.ToString("#,##0") : "")}</td>");
                sb.Append("<td></td>");
                sb.Append($"<td class='num bold'>{(grandOtherExp != 0 ? grandOtherExp.ToString("#,##0") : "")}</td>");
                sb.Append($"<td class='num bold'>{grandNet:#,##0}</td>");
                sb.Append("</tr></tfoot></table></body></html>");

                return Content(sb.ToString(), "text/html");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}", "text/html");
            }
        }

        [HttpGet]
        public IActionResult ExportExcel(DateTime? fromDate, DateTime? toDate, string? partyCode, string? reportType, decimal? biltyNo, string? vehicleNo, string? station, int? companyId, string? pchalno, string? pcocode, string? fdate, string? tdate, string? mode = null)
        {
            int selectedCompanyId = GetCompanyId(companyId, pcocode);
            bool isListMode = string.Equals(mode, "List", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(pchalno);

            if (isListMode)
            {
                try
                {
                    DataTable dt = GetChallanListData(pchalno, selectedCompanyId);

                    if (dt == null || dt.Rows.Count == 0)
                    {
                        return Content("No data found to export.");
                    }

                    var sbList = new StringBuilder();
                    string compName = (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company";
                    sbList.AppendLine($"\"{compName}\"");
                    sbList.AppendLine("\"Challans\"");
                    string pchalInfo = !string.IsNullOrWhiteSpace(pchalno) ? $",\"Challan No:\",\"{pchalno}\"" : "";
                    sbList.AppendLine($"\"Total Records:\",\"{dt.Rows.Count}\"{pchalInfo}");
                    sbList.AppendLine();

                    sbList.AppendLine("Docno,Docdate,Chal#,Branch,Station,Vehicle,Driver,Transporter,Tot AMT,Narration");

                    decimal totAmt = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        string docDate = row["DocDate"] != DBNull.Value ? Convert.ToDateTime(row["DocDate"]).ToString("dd/MM/yy") : "";
                        decimal amt = row["TotAmt"] != DBNull.Value ? Convert.ToDecimal(row["TotAmt"]) : 0;
                        totAmt += amt;

                        sbList.AppendLine($"{EscapeCsv(row["DocNo"]?.ToString())},{docDate},{row["ChalNo"]},{row["Branch"]},{EscapeCsv(row["Station"]?.ToString())},{EscapeCsv(row["Vehicle"]?.ToString())},{EscapeCsv(row["Driver"]?.ToString())},{EscapeCsv(row["Transporter"]?.ToString())},{(amt != 0 ? amt.ToString("#,##0") : "")},{EscapeCsv(row["Narration"]?.ToString())}");
                    }

                    sbList.AppendLine($"\"TOTAL\",,,,,,,,{totAmt:#,##0},");

                    byte[] bytes = Encoding.UTF8.GetBytes(sbList.ToString());
                    string chalSuffix = !string.IsNullOrWhiteSpace(pchalno) ? $"_Chal_{pchalno.Trim()}" : "";
                    return File(bytes, "text/csv", $"ChallanList{chalSuffix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                }
                catch (Exception ex)
                {
                    return Content($"ERROR: {ex.Message}");
                }
            }

            // Challan Report CSV export — using ChallanHead+ChallanDet with correct columns
            DateTime? effFromDate = !string.IsNullOrEmpty(fdate) && DateTime.TryParse(fdate, out DateTime fd) ? fd : fromDate;
            DateTime? effToDate = !string.IsNullOrEmpty(tdate) && DateTime.TryParse(tdate, out DateTime td) ? td : toDate;

            try
            {
                DataTable dt = GetChallanReportData(effFromDate, effToDate, selectedCompanyId, pchalno);

                if (dt == null || dt.Rows.Count == 0)
                {
                    return Content("No data found to export.");
                }

                var sb = new StringBuilder();
                string compName = (dt.Rows.Count > 0 ? dt.Rows[0]["CompanyName"]?.ToString() : null) ?? "Company";
                sb.AppendLine($"\"{compName}\"");
                sb.AppendLine("\"Challan Report\"");
                sb.AppendLine($"\"Date From:\",\"{effFromDate:dd-MM-yyyy}\",\"Date To:\",\"{effToDate:dd-MM-yyyy}\"");
                sb.AppendLine();

                sb.AppendLine("Date,No,Station,Vehicle,Transporter,Delivery Amt,Lifter Amt,Billti Amt,To paid,B#/ Party,Party Exp,B# Local,Local Amt,B#/ Ex Other,Other Exp,Net Amt");

                decimal totDel = 0, totLif = 0, totBil = 0, totTopaid = 0, totPExp = 0, totLoc = 0, totOth = 0, totNet = 0;

                foreach (DataRow row in dt.Rows)
                {
                    string docDate = row["DocDate"] != DBNull.Value ? Convert.ToDateTime(row["DocDate"]).ToString("dd/MM/yy") : "";
                    decimal? deliveryAmt = row["DeliveryAmt"] != DBNull.Value ? Convert.ToDecimal(row["DeliveryAmt"]) : null;
                    decimal? lifterAmt = row["LifterAmt"] != DBNull.Value ? Convert.ToDecimal(row["LifterAmt"]) : null;
                    decimal? billtiAmt = row["BilltiAmt"] != DBNull.Value ? Convert.ToDecimal(row["BilltiAmt"]) : null;
                    decimal? toPaid = row["ToPaid"] != DBNull.Value ? Convert.ToDecimal(row["ToPaid"]) : null;
                    string bParty = row["BParty"]?.ToString() ?? "";
                    decimal? partyExp = row["PartyExp"] != DBNull.Value ? Convert.ToDecimal(row["PartyExp"]) : null;
                    string bLocal = row["BLocal"]?.ToString() ?? "";
                    decimal? localAmt = row["LocalAmt"] != DBNull.Value ? Convert.ToDecimal(row["LocalAmt"]) : null;
                    string bOtherEx = row["BOtherEx"]?.ToString() ?? "";
                    decimal? otherExp = row["OtherExp"] != DBNull.Value ? Convert.ToDecimal(row["OtherExp"]) : null;
                    decimal? netAmt = row["NetAmt"] != DBNull.Value ? Convert.ToDecimal(row["NetAmt"]) : null;

                    if (deliveryAmt.HasValue) totDel += deliveryAmt.Value;
                    if (lifterAmt.HasValue) totLif += lifterAmt.Value;
                    if (billtiAmt.HasValue) totBil += billtiAmt.Value;
                    if (toPaid.HasValue) totTopaid += toPaid.Value;
                    if (partyExp.HasValue) totPExp += partyExp.Value;
                    if (localAmt.HasValue) totLoc += localAmt.Value;
                    if (otherExp.HasValue) totOth += otherExp.Value;
                    if (netAmt.HasValue) totNet += netAmt.Value;

                    sb.AppendLine($"{docDate},{EscapeCsv(row["No"]?.ToString())},{EscapeCsv(row["Station"]?.ToString())},{EscapeCsv(row["Vehicle"]?.ToString())},{EscapeCsv(row["Transporter"]?.ToString())},{(deliveryAmt.HasValue ? deliveryAmt.Value.ToString() : "")},{(lifterAmt.HasValue ? lifterAmt.Value.ToString() : "")},{(billtiAmt.HasValue ? billtiAmt.Value.ToString() : "")},{(toPaid.HasValue ? toPaid.Value.ToString() : "")},{EscapeCsv(bParty)},{(partyExp.HasValue ? partyExp.Value.ToString() : "0")},{EscapeCsv(bLocal)},{(localAmt.HasValue && localAmt.Value != 0 ? localAmt.Value.ToString() : "")},{EscapeCsv(bOtherEx)},{(otherExp.HasValue && otherExp.Value != 0 ? otherExp.Value.ToString() : "")},{(netAmt.HasValue ? netAmt.Value.ToString() : "0")}");
                }

                sb.AppendLine($"\"TOTAL\",,,,{totDel},{(totLif != 0 ? totLif.ToString() : "")},{totBil},{totTopaid},,{totPExp},,{(totLoc != 0 ? totLoc.ToString() : "")},,{(totOth != 0 ? totOth.ToString() : "")},{totNet}");

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"challanreport_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                return Content($"ERROR: {ex.Message}\n\nSTACK: {ex.StackTrace}");
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
