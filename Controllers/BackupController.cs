using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

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

                EnsureDirectoryPermissions(backupsDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{dbName}_Backup_{timestamp}.bak";
                string fullPath = Path.Combine(backupsDir, fileName);

                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();

                    // Query SQL Server's own default backup directory if available
                    string? sqlDefaultBackupPath = null;
                    try
                    {
                        using (var cmdPath = new SqlCommand("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(512))", conn))
                        {
                            var res = await cmdPath.ExecuteScalarAsync();
                            if (res != null && res != DBNull.Value && !string.IsNullOrWhiteSpace(res.ToString()))
                            {
                                sqlDefaultBackupPath = res.ToString();
                            }
                        }
                    }
                    catch
                    {
                        // Ignore query error for default backup path
                    }

                    bool backupSuccess = false;

                    // 1. First attempt: Direct backup to application Backups directory
                    try
                    {
                        string sql = $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH INIT, COPY_ONLY;";
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.CommandTimeout = 300; // 5 minutes
                            cmd.Parameters.AddWithValue("@path", fullPath);
                            await cmd.ExecuteNonQueryAsync();
                        }
                        backupSuccess = true;
                    }
                    catch (SqlException ex) when (!string.IsNullOrWhiteSpace(sqlDefaultBackupPath) &&
                                                  (ex.Number == 3201 || ex.Message.Contains("error 5") || ex.Message.IndexOf("access is denied", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        // 2. Fallback: Backup to SQL Server default backup directory where SQL Server has guaranteed write rights
                        string sqlDefaultFullPath = Path.Combine(sqlDefaultBackupPath, fileName);
                        string sqlFallback = $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH INIT, COPY_ONLY;";
                        using (var cmdFallback = new SqlCommand(sqlFallback, conn))
                        {
                            cmdFallback.CommandTimeout = 300;
                            cmdFallback.Parameters.AddWithValue("@path", sqlDefaultFullPath);
                            await cmdFallback.ExecuteNonQueryAsync();
                        }

                        // Copy or move the generated backup file from SQL directory to web application Backups folder
                        try
                        {
                            if (System.IO.File.Exists(sqlDefaultFullPath))
                            {
                                System.IO.File.Copy(sqlDefaultFullPath, fullPath, true);
                                try { System.IO.File.Delete(sqlDefaultFullPath); } catch { }
                                backupSuccess = true;
                            }
                        }
                        catch
                        {
                            // If web app cannot copy due to IIS permissions on SQL folder, the file is in sqlDefaultBackupPath
                            TempData["SuccessMessage"] = $"Backup created in SQL Server default directory: {sqlDefaultFullPath}";
                            return RedirectToAction(nameof(Index));
                        }
                    }

                    if (backupSuccess)
                    {
                        TempData["SuccessMessage"] = $"Backup created successfully: {fileName}";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to create backup: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private void EnsureDirectoryPermissions(string path)
        {
            try
            {
                if (!OperatingSystem.IsWindows()) return;
                var dirInfo = new DirectoryInfo(path);
                var dirSecurity = dirInfo.GetAccessControl();
                dirSecurity.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    FileSystemRights.Modify,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                dirInfo.SetAccessControl(dirSecurity);
            }
            catch
            {
                // Ignore if process lacks privilege to set ACL
            }
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
