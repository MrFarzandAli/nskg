
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Models;
using Nskg.Repositories.Interfaces;

namespace Nskg.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AcParaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;

        public AcParaController(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        // ================= INDEX =================
        public IActionResult Index()
        {
            ViewBag.AccountType = _context.Actype.ToList();
            return View(_context.AcPara.ToList());
        }

        // ================= CREATE =================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AcPara model)
        {
            try
            {
                if (_context.AcPara.Any(x => x.Accode == model.Accode))
                {
                    return Json(new { success = false, message = "Account code already exists!" });
                }

                _context.AcPara.Add(model);
                _context.SaveChanges();

                await _audit.LogAsync(
                    "Create",
                    "AcPara",
                    model.Id.ToString(),
                    $"Created Account: {model.Accode}"
                );

                // return Json(new { success = true, message = "Created successfully!" });
                TempData["SuccessMessage"] = "Created successfully!";
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // 🔥 ERROR LOG
                await _audit.LogAsync(
                    "Error",
                    "AccountPara",
                    "0",
                    ex.Message
                );

                TempData["ErrorMessage"] = "❌ Failed to create AccountPara!";
                return View(model);
            }
        }

        // ================= EDIT =================
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] AcPara model)
        {
            try
            {
                var data = _context.AcPara.Find(model.Id);

                if (data == null)
                    return Json(new { success = false, message = "Not found!" });

                data.Accode = model.Accode;
                data.Acname = model.Acname;
                data.Actype = model.Actype;
                data.Parent = model.Parent;
                data.Opening = model.Opening;

                _context.SaveChanges();

                await _audit.LogAsync(
                    "Update",
                    "AcPara",
                    model.Id.ToString(),
                    $"Updated Account: {model.Accode}"
                );

                // return Json(new { success = true, message = "Updated successfully!" });
                TempData["SuccessMessage"] = "Updated successfully!";
                return Json(new { success = true });

            }
            catch (Exception ex)
            {
                // 🔥 ERROR LOG
                await _audit.LogAsync(
                    "Error",
                    "AccountPara",
                    model.Id.ToString(),
                    ex.Message
                );

                TempData["ErrorMessage"] = "❌ Failed to update AccountPara!";
                return View(model);
            }
        }

        // ================= DELETE =================

        [HttpDelete]
        [ValidateAntiForgeryToken]   // 🔥 ADD THIS
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var data = _context.AcPara.Find(id);

                if (data == null)
                    return Json(new { success = false, message = "Not found!" });

                _context.AcPara.Remove(data);
                _context.SaveChanges();

                await _audit.LogAsync(
                    "Delete",
                    "AcPara",
                    id.ToString(),
                    $"Deleted Account: {data.Accode}"
                );

                return Json(new { success = true, message = "Deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "AcPara", id.ToString(), ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ================= GET SINGLE =================
        public IActionResult Get(int id)
        {
            var data = _context.AcPara.Find(id);
            return Json(data);
        }

        // ================= GET ACCOUNTS =================
        public IActionResult GetAccounts(string type)
        {
            if (type == "P")
            {
                var data = _context.GLChart1
                    .Select(x => new
                    {
                        accode = x.AC1,
                        acname = x.Name
                    }).ToList();

                return Json(data);
            }
            else if (type == "C")
            {
                var data = _context.GLChart3
                    .Select(x => new
                    {
                        accode = x.ACC,
                        acname = x.Name
                    }).ToList();

                return Json(data);
            }

            return Json(new List<object>());
        }
    }
}
