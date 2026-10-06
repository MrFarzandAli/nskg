using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.IO;

namespace Nskg.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BackupController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public BackupController(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;
        }

        public IActionResult Index()
        {
            var backupsDir = Path.Combine(_env.ContentRootPath, "Backups");
            if (!Directory.Exists(backupsDir))
            {
                Directory.CreateDirectory(backupsDir);
            }

            var dirInfo = new DirectoryInfo(backupsDir);
            var files = dirInfo.GetFiles("*.bak")
                               .OrderByDescending(f => f.CreationTime)
                               .ToList();

            return View(files);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBackup()
        {
            try
            {
                string connStr = _configuration.GetConnectionString("DefaultConnection")!;
                var builder = new SqlConnectionStringBuilder(connStr);
                string dbName = string.IsNullOrEmpty(builder.InitialCatalog) ? "nskg_live" : builder.InitialCatalog;

                var backupsDir = Path.Combine(_env.ContentRootPath, "Backups");
                if (!Directory.Exists(backupsDir))
                {
                    Directory.CreateDirectory(backupsDir);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{dbName}_Backup_{timestamp}.bak";
                string fullPath = Path.Combine(backupsDir, fileName);

                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    string sql = $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH INIT, COPY_ONLY;";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.CommandTimeout = 300; // 5 minutes
                        cmd.Parameters.AddWithValue("@path", fullPath);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                TempData["SuccessMessage"] = $"Backup created successfully: {fileName}";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to create backup: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Download(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return BadRequest("Invalid file name.");
            }

            // Security check against directory traversal
            var safeFileName = Path.GetFileName(fileName);
            var backupsDir = Path.Combine(_env.ContentRootPath, "Backups");
            var fullPath = Path.Combine(backupsDir, safeFileName);

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound("Backup file not found.");
            }

            return PhysicalFile(fullPath, "application/octet-stream", safeFileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string fileName)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    var safeFileName = Path.GetFileName(fileName);
                    var backupsDir = Path.Combine(_env.ContentRootPath, "Backups");
                    var fullPath = Path.Combine(backupsDir, safeFileName);

                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                        TempData["SuccessMessage"] = $"Backup file {safeFileName} deleted.";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting file: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
