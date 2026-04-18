
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Extensions;
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
            ViewBag.AccountType = _context.Actype
                .Select(x => new
                {
                    x.ACTYPE,
                    x.ACNAME
                })
                .ToList();

            var data = _context.AcPara
                .Include(x => x.Actype)
                .Include(x => x.GLChart1)
                .Include(x => x.GLChart3)
                .ToList();

            return View(data);
            ViewBag.AccountType = _context.Actype.ToList();
            return View(_context.AcPara.ToList());
        }

        // ================= CREATE =================
        [HttpPost]
        public IActionResult Create([FromBody] AcPara model)
        {
            // ✅ Actype validation
            if (!string.IsNullOrEmpty(model.ActypeCode) &&
                !_context.Actype.Any(x => x.ACTYPE == model.ActypeCode))
                return BadRequest("Invalid Account Type");

            model.Cocode = User.GetCompanyId().ToString();
            // ✅ Parent/Child logic
            if (model.Parent == "P")
            {
                if (model.GLChart1Id == null)
                    return BadRequest("Parent account required");

                var gl = _context.GLChart1.Find(model.GLChart1Id);
                model.Accode = gl?.AC1;
                model.Acname = gl?.Name;

                model.GLChart3Id = null;
            }
            else if (model.Parent == "C")
            {
                if (model.GLChart3Id == null)
                    return BadRequest("Child account required");
        public async Task<IActionResult> Create([FromBody] AcPara model)
        {
            try
            {
                if (_context.AcPara.Any(x => x.Accode == model.Accode))
                {
                    return Json(new { success = false, message = "Account code already exists!" });
                }

                var gl = _context.GLChart3.Find(model.GLChart3Id);
                model.Accode = gl?.ACC;
                model.Acname = gl?.Name;

                model.GLChart1Id = null;
            }
            
            if (_context.AcPara.Any(x => x.Accode == model.Accode))
                return BadRequest("Account code already exists!");

            _context.AcPara.Add(model);
            _context.SaveChanges();
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
            var data = _context.AcPara.Find(model.Id);
            if (data == null) return NotFound();
            try
            {
                var data = _context.AcPara.Find(model.Id);

                if (data == null)
                    return Json(new { success = false, message = "Not found!" });

            if (_context.AcPara.Any(x => x.Accode == model.Accode && x.Id != model.Id))
                return BadRequest("Account code already exists!");

            // ✅ Actype validation
            if (!string.IsNullOrEmpty(model.ActypeCode) &&
                !_context.Actype.Any(x => x.ACTYPE == model.ActypeCode))
                return BadRequest("Invalid Account Type");

            data.ActypeCode = model.ActypeCode;
            data.Parent = model.Parent;
            data.Opening = model.Opening;
                data.Accode = model.Accode;
                data.Acname = model.Acname;
                data.Actype = model.Actype;
                data.Parent = model.Parent;
                data.Opening = model.Opening;

            if (model.Parent == "P")
            {
                var gl = _context.GLChart1.Find(model.GLChart1Id);

                data.GLChart1Id = model.GLChart1Id;
                data.GLChart3Id = null;

                data.Accode = gl?.AC1;
                data.Acname = gl?.Name;
            }
            else if (model.Parent == "C")
            {
                var gl = _context.GLChart3.Find(model.GLChart3Id);

                data.GLChart3Id = model.GLChart3Id;
                data.GLChart1Id = null;

                data.Accode = gl?.ACC;
                data.Acname = gl?.Name;
            }

            _context.SaveChanges();
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


        [HttpPost]
        public IActionResult Delete(int id)
                TempData["ErrorMessage"] = "❌ Failed to update AccountPara!";
                return View(model);
            }
        }

        // ================= DELETE =================

        [HttpDelete]
        [ValidateAntiForgeryToken]   // 🔥 ADD THIS
        public async Task<IActionResult> Delete(int id)
        {
            var data = _context.AcPara.Find(id);
            if (data == null) return NotFound();

            var used = _context.VoDet.Any(x => x.Acc == data.Accode);
            if (used)
                return BadRequest("Account used in transactions");
            try
            {
                var data = _context.AcPara.Find(id);

            _context.AcPara.Remove(data);
            _context.SaveChanges();
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
                        accode = x.AC1,     // ✅ normalize name
                        acname = x.Name,
                        glId = x.Id
                    })
                    .ToList();
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
                        accode = x.ACC,     // ✅ normalize name
                        acname = x.Name,
                        glId = x.Id
                    })
                    .ToList();
                        accode = x.ACC,
                        acname = x.Name
                    }).ToList();

                return Json(data);
            }

            return Json(new List<object>());
        }
    }
}
