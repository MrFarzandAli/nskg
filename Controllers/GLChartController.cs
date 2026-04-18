
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nskg.Data;
using Nskg.Extensions;
using Nskg.Models;
using Nskg.Models.ViewModels;
using Nskg.Repositories.Interfaces;

namespace Nskg.Controllers
{
    public class GLChartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _audit;


        public GLChartController(ApplicationDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public IActionResult Index()
        {           
            return View(new ChartVM
            {
                Accounts = _context.GLChart1.ToList(),
                Categories = _context.AccCat
                .Select(x => new SelectListItem
                {
                    Value = x.CatCode,     // or x.Id if you have Id
                    Text = x.Category
                })
                .ToList()
            });
        }

        public IActionResult GetDetails(int accountId)
        {
            var data = _context.GLChart3
                .Where(x => x.GLChart1Id == accountId)
                .ToList();

            return PartialView("_GLChart3List", data);
        }

       
        [HttpPost]
        public async Task<IActionResult> AddAccount(GLChart1 model)
        {
            try
            {
                model.AC1 = GenerateAC1();
                model.CoCode = User.GetCompanyId().ToString();

                _context.GLChart1.Add(model);
                _context.SaveChanges();

                await _audit.LogAsync("Create", "GLChart1", model.Id.ToString(), $"Created Account: {model.Name}");

                TempData["SuccessMessage"] = "Account created successfully!";
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "GLChart1", "0", ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAccount(GLChart1 model)
        {
            try
            {
                var data = _context.GLChart1.Find(model.Id);
                if (data == null)
                    return Json(new { success = false, message = "Not found!" });

                data.Name = model.Name;
                data.CType = model.CType;
                data.AcType = model.AcType;

                _context.SaveChanges();

                await _audit.LogAsync("Update", "GLChart1", model.Id.ToString(), $"Updated Account: {model.Name}");

                TempData["SuccessMessage"] = "Account updated successfully!";
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "GLChart1", model.Id.ToString(), ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }

       
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            try
            {
                var data = _context.GLChart1.Find(id);
                if (data == null)
                    return Json(new { success = false, message = "Not found!" });

                _context.GLChart1.Remove(data);
                _context.SaveChanges();

                await _audit.LogAsync("Delete", "GLChart1", id.ToString(), $"Deleted Account: {data.Name}");

                TempData["SuccessMessage"] = "Account deleted successfully!";
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "GLChart1", id.ToString(), ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // DETAIL
       
        [HttpPost]
        public async Task<IActionResult> AddDetail(GLChart3 model)
        {
            try
            {
                var account = _context.GLChart1.Find(model.GLChart1Id);

                model.AC1 = account.AC1;
                model.AC3 = GenerateAC3(model.GLChart1Id);
                model.ACC = account.AC1 + GenerateAC3(model.GLChart1Id);
                model.AcType = account.AcType;
                model.CType = account.CType;
                model.CHName = account.Name;
                model.CoCode = User.GetCompanyId().ToString();

                _context.GLChart3.Add(model);
                _context.SaveChanges();

                await _audit.LogAsync("Create", "GLChart3", model.Id.ToString(), $"Created Detail: {model.Name}");

                return Json(new { success = true, message = "Detail added successfully!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "GLChart3", "0", ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetDetail(int id)
        {
            var data = _context.GLChart3.Find(id);

            return Json(new
            {
                id = data.Id,
                name = data.Name,
                glChart1Id = data.GLChart1Id
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateDetail(GLChart3 model)
        {
            try
            {
                var data = _context.GLChart3.Find(model.Id);
                if (data == null)
                    return Json(new { success = false, message = "Not found!" });

                data.Name = model.Name;
                _context.SaveChanges();

                await _audit.LogAsync("Update", "GLChart3", model.Id.ToString(), $"Updated Detail: {model.Name}");

                return Json(new { success = true, message = "Detail updated!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "GLChart3", model.Id.ToString(), ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }


      
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDetail(int id)
        {
            try
            {
                var data = _context.GLChart3.Find(id);
                if (data == null)
                    return Json(new { success = false, message = "Not found!" });

                _context.GLChart3.Remove(data);
                _context.SaveChanges();

                await _audit.LogAsync("Delete", "GLChart3", id.ToString(), $"Deleted Detail: {data.Name}");

                return Json(new { success = true, message = "Deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "GLChart3", id.ToString(), ex.Message);
                return Json(new { success = false, message = ex.Message });
            }
        }
        private string GenerateAC1()
        {
            var last = _context.GLChart1
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            if (last == null)
                return "01";

            int next = int.Parse(last.AC1) + 1;
            return next.ToString("D2");
        }

        private string GenerateAC3(int accountId)
        {
            var last = _context.GLChart3
                .Where(x => x.GLChart1Id == accountId)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            if (last == null)
                return "001";

            int next = int.Parse(last.AC3) + 1;
            return next.ToString("D3");
        }
    }
}
