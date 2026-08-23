using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Extensions;
using Nskg.Models;
using Nskg.Models.ViewModels;
using Nskg.Repositories.Interfaces;

namespace Nskg.Controllers
{
    public class AcParaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;   // ✅ ADD

        public AcParaController(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;   // ✅ ADD
        }
        private string GetUser()
        {
            return User?.Identity?.Name ?? "System";
        }

        public IActionResult Index()
        {
            int companyId = User.GetCompanyId();
            string companyCode = User.GetCompanyCode();

            ViewBag.AccountType = _context.Actype.Where(x => !x.IsDeleted)
                .Select(x => new
                {
                    x.ACTYPE,
                    x.ACNAME
                })
                .ToList();

            var data = _context.AcPara
                .Where(x => !x.IsDeleted && (x.CompanyId == companyId || x.Cocode == companyCode || x.Cocode == companyId.ToString()))
                .Include(x => x.Actype)
                .Include(x => x.GLChart1)
                .Include(x => x.GLChart3)
                .ToList();

            return View(data);
        }


        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AcPara model)
        {
            try
            {
                if (!string.IsNullOrEmpty(model.ActypeCode) &&
                    !_context.Actype.Any(x => x.ACTYPE == model.ActypeCode))
                    return Json(new { success = false, message = "Invalid Account Type" });

                int companyId = User.GetCompanyId();
                string companyCode = User.GetCompanyCode();

                model.CompanyId = companyId;
                model.Cocode = companyCode;

                if (model.Parent == "P")
                {
                    var gl = _context.GLChart1.Find(model.GLChart1Id);
                    model.Accode = gl?.AC1;
                    model.Acname = gl?.Name;
                    model.GLChart3Id = null;
                }
                else if (model.Parent == "C")
                {
                    var gl = _context.GLChart3.Find(model.GLChart3Id);
                    model.Accode = gl?.ACC;
                    model.Acname = gl?.Name;
                    model.GLChart1Id = null;
                }

                if (_context.AcPara.Any(x => x.Accode == model.Accode && (x.CompanyId == companyId || x.Cocode == companyCode)))
                    return Json(new { success = false, message = "Account code already exists!" });
                // 🔥 SET AUDIT FIELDS
                model.CreatedOn = DateTime.Now;
                model.CreatedBy = GetUser();
                model.IsDeleted = false;
                _context.AcPara.Add(model);
                _context.SaveChanges();

                await _audit.LogAsync("Create", "AcPara", model.Id.ToString(), $"Created Account: {model.Acname}");

                return Json(new { success = true, message = "Account created successfully!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "AcPara", "0", ex.Message);
                return Json(new { success = false, message = "Something went wrong!" });
            }
        }



        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] AcPara model)
        {
            try
            {
                var data = _context.AcPara.Find(model.Id);
                if (data == null)
                    return Json(new { success = false, message = "Not found!" });

                int companyId = User.GetCompanyId();
                string companyCode = User.GetCompanyCode();

                if (_context.AcPara.Any(x => x.Accode == model.Accode && x.Id != model.Id && (x.CompanyId == companyId || x.Cocode == companyCode)))
                    return Json(new { success = false, message = "Account code already exists!" });

                if (!string.IsNullOrEmpty(model.ActypeCode) &&
                    !_context.Actype.Any(x => x.ACTYPE == model.ActypeCode))
                    return Json(new { success = false, message = "Invalid Account Type" });

                data.ActypeCode = model.ActypeCode;
                data.Parent = model.Parent;
                data.Opening = model.Opening;
                data.CompanyId = companyId;
                data.Cocode = companyCode;

                if (model.Parent == "P")
                {
                    var gl = _context.GLChart1.Find(model.GLChart1Id);
                    data.GLChart1Id = model.GLChart1Id;
                    data.GLChart3Id = null;
                    data.Accode = gl?.AC1;
                    data.Acname = gl?.Name;
                }
                else
                {
                    var gl = _context.GLChart3.Find(model.GLChart3Id);
                    data.GLChart3Id = model.GLChart3Id;
                    data.GLChart1Id = null;
                    data.Accode = gl?.ACC;
                    data.Acname = gl?.Name;
                }
                // 🔥 AUDIT UPDATE
                data.ModifiedOn = DateTime.Now;
                data.ModifiedBy = GetUser();
                _context.SaveChanges();

                await _audit.LogAsync("Update", "AcPara", model.Id.ToString(), $"Updated Account: {data.Acname}");

                return Json(new { success = true, message = "Updated successfully!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "AcPara", model.Id.ToString(), ex.Message);
                return Json(new { success = false, message = "Update failed!" });
            }
        }

        
        [HttpPost]
        public IActionResult Delete(int id)
        {
            var data = _context.AcPara.Find(id);
            if (data == null)
                return Json(new { success = false, message = "Not found!" });

            var used = _context.VoDet.Any(x => x.Acc == data.Accode);
            if (used)
                return Json(new { success = false, message = "Account used in transactions" });
            // ✅ SOFT DELETE
            data.IsDeleted = true;
            data.ModifiedOn = DateTime.Now;
            data.ModifiedBy = User?.Identity?.Name ?? "System";

            _context.AcPara.Update(data);
            //  _context.AcPara.Remove(data);
            _context.SaveChanges();

            return Json(new { success = true, message = "Deleted successfully!" });
        }
        public IActionResult GetAccounts(string type)
        {
            int companyId = User.GetCompanyId();
            string companyCode = User.GetCompanyCode();

            if (type == "P")
            {
                var data = _context.GLChart1
                    .Where(x => x.CompanyId == companyId || x.CoCode == companyCode)
                    .Select(x => new
                    {
                        accode = x.AC1,     // ✅ normalize name
                        acname = x.Name,
                        glId = x.Id
                    })
                    .ToList();

                return Json(data);
            }
            else if (type == "C")
            {
                var data = _context.GLChart3
                    .Where(x => x.CoCode == companyCode || x.CoCode == companyId.ToString())
                    .Select(x => new
                    {
                        accode = x.ACC,     // ✅ normalize name
                        acname = x.Name,
                        glId = x.Id
                    })
                    .ToList();

                return Json(data);
            }

            return Json(new List<object>());
        }

        public IActionResult Get(int id)
        {
            var data = _context.AcPara
                .Include(x => x.GLChart1)
                .Include(x => x.GLChart3)
                .FirstOrDefault(x => x.Id == id);

            if (data == null) return Json(null);

            return Json(new
            {
                data.Id,
                data.Parent,
                data.Actype,
                data.Opening,
                glId = data.Parent == "P" ? data.GLChart1Id : data.GLChart3Id
            });
        }
    }
}
