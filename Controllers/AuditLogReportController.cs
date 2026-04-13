//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Hosting;
//using Microsoft.Reporting.NETCore;
//using System.Data;
//using Microsoft.Data.SqlClient;
//using System.IO;
//using Microsoft.Extensions.Configuration;

//public class AuditLogReportController : Controller
//{
//    private readonly IConfiguration _config;
//    private readonly IWebHostEnvironment _env;

//    public AuditLogReportController(IConfiguration config, IWebHostEnvironment env)
//    {
//        _config = config;
//        _env = env;
//}

//    // GET: Display form
//    [HttpGet]
//    public IActionResult Index()
//    {
//        return View();
//    }
//    private DataTable GetAuditLogs(DateTime fromDate, DateTime toDate, int? companyId, int? financialYearId)
//    {
//        DataTable dt = new DataTable();

//        dt.Columns.Add("Id", typeof(int));
//        dt.Columns.Add("UserName", typeof(string));
//        dt.Columns.Add("Action", typeof(string));
//        dt.Columns.Add("TableName", typeof(string));
//        dt.Columns.Add("RecordId", typeof(string));
//        dt.Columns.Add("Details", typeof(string));
//        dt.Columns.Add("CreatedAt", typeof(DateTime));
//        dt.Columns.Add("CompanyId", typeof(int));
//        dt.Columns.Add("FinancialYearId", typeof(int));

//        string connString = _config.GetConnectionString("DefaultConnection");

//        using (SqlConnection con = new SqlConnection(connString))
//        {
//            using (SqlCommand cmd = new SqlCommand("sp_GetAuditLogs", con))
//            {
//                cmd.CommandType = CommandType.StoredProcedure;

//                cmd.Parameters.AddWithValue("@FromDate", fromDate);
//                cmd.Parameters.AddWithValue("@ToDate", toDate);
//                cmd.Parameters.AddWithValue("@CompanyId", companyId ?? (object)DBNull.Value);
//                cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId ?? (object)DBNull.Value);

//                con.Open();

//                using (SqlDataReader reader = cmd.ExecuteReader())
//                {
//                    while (reader.Read())
//                    {
//                        dt.Rows.Add(
//                            reader["Id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Id"]),
//                            reader["UserName"] == DBNull.Value ? "" : reader["UserName"].ToString(),
//                            reader["Action"] == DBNull.Value ? "" : reader["Action"].ToString(),
//                            reader["TableName"] == DBNull.Value ? "" : reader["TableName"].ToString(),
//                            reader["RecordId"] == DBNull.Value ? "" : reader["RecordId"].ToString(),
//                            reader["Details"] == DBNull.Value ? "" : reader["Details"].ToString(),
//                            reader["CreatedAt"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader["CreatedAt"]),
//                            reader["CompanyId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CompanyId"]),
//                            reader["FinancialYearId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["FinancialYearId"])
//                        );
//                    }
//                }
//            }
//        }

//        return dt;
//    }
//    [HttpGet]
//    public IActionResult GeneratePDF(DateTime fromDate, DateTime toDate, int? companyId, int? financialYearId)
//    {
//        try
//        {
//            DataTable dt = GetAuditLogs(fromDate, toDate, companyId, financialYearId);

//            using (LocalReport report = new LocalReport())
//            {
//                string reportPath = Path.Combine(_env.WebRootPath, "Reports", "AuditLogrpt.rdlc");
//                report.ReportPath = reportPath;
//                report.DataSources.Clear();
//                report.DataSources.Add(new ReportDataSource("DSAuditLog", dt));

//                // Use the available single-argument overload
//                byte[] pdfBytes = report.Render("PDF");

//                return File(pdfBytes, "application/pdf");
//            }
//        }
//        catch (Exception ex)
//        {
//            return Content("Error: " + ex.Message);
//        }
//    }
//}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Reporting.NETCore;
using System.Data;
using System.IO;

namespace Nskg.Controllers  // Apna actual namespace dalo
{
    [Authorize(Roles = "Admin")]
    public class AuditLogReportController : Controller
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public AuditLogReportController(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
        }

        // GET: Display form
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        private DataTable GetAuditLogs(DateTime fromDate, DateTime toDate, int? companyId, int? financialYearId)
        {
            DataTable dt = new DataTable();

            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("UserName", typeof(string));
            dt.Columns.Add("Action", typeof(string));
            dt.Columns.Add("TableName", typeof(string));
            dt.Columns.Add("RecordId", typeof(string));
            dt.Columns.Add("Details", typeof(string));
            dt.Columns.Add("CreatedAt", typeof(DateTime));
            dt.Columns.Add("CompanyId", typeof(int));
            dt.Columns.Add("FinancialYearId", typeof(int));

            string connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(connString))
            {
                using (SqlCommand cmd = new SqlCommand("sp_GetAuditLogs", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);
                    cmd.Parameters.AddWithValue("@CompanyId", companyId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@FinancialYearId", financialYearId ?? (object)DBNull.Value);

                    con.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dt.Rows.Add(
                                reader["Id"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Id"]),
                                reader["UserName"] == DBNull.Value ? "" : reader["UserName"].ToString(),
                                reader["Action"] == DBNull.Value ? "" : reader["Action"].ToString(),
                                reader["TableName"] == DBNull.Value ? "" : reader["TableName"].ToString(),
                                reader["RecordId"] == DBNull.Value ? "" : reader["RecordId"].ToString(),
                                reader["Details"] == DBNull.Value ? "" : reader["Details"].ToString(),
                                reader["CreatedAt"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader["CreatedAt"]),
                                reader["CompanyId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CompanyId"]),
                                reader["FinancialYearId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["FinancialYearId"])
                            );
                        }
                    }
                }
            }

            return dt;
        }

        [HttpGet]
        public IActionResult GeneratePDF(DateTime fromDate, DateTime toDate, int? companyId, int? financialYearId)
        {
            try
            {
                DataTable dt = GetAuditLogs(fromDate, toDate, companyId, financialYearId);

                if (dt.Rows.Count == 0)
                {
                    return Content("No data found for selected date range.");
                }

                string reportPath = Path.Combine(_env.WebRootPath, "Reports", "AuditLogrpt.rdlc");

                // Check if file exists
                if (!System.IO.File.Exists(reportPath))
                {
                    return Content($"Report file not found at: {reportPath}");
                }

                using (LocalReport report = new LocalReport())
                {
                    report.ReportPath = reportPath;
                    report.DataSources.Clear();
                    report.DataSources.Add(new ReportDataSource("DSAuditLog", dt));

                    // Important: Use two-parameter overload
                    byte[] pdfBytes = report.Render("PDF");
                    return File(pdfBytes, "application/pdf", "AuditLogReport.pdf");
                }
            }
            catch (Exception ex)
            {
                // Detailed error
                return Content($"Error: {ex.Message}\n\nStack Trace: {ex.StackTrace}");
            }
        }
    }
}