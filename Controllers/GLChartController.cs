
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nskg.Data;
using Nskg.Extensions;
using Nskg.Models;
using Nskg.Models.ViewModels;

namespace Nskg.Controllers
{
    public class GLChartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GLChartController(ApplicationDbContext context)
        {
            _context = context;
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
        public IActionResult AddAccount(GLChart1 model)
        {
            model.AC1 = GenerateAC1();
            model.CoCode = User.GetCompanyId().ToString();

            _context.GLChart1.Add(model);
            _context.SaveChanges();

            return Ok();
        }

        [HttpPost]
        public IActionResult UpdateAccount(GLChart1 model)
        {
            var data = _context.GLChart1.Find(model.Id);
            if (data == null) return NotFound();

            data.AC1 = model.AC1;
            data.Name = model.Name;

            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        public IActionResult DeleteAccount(int id)
        {
            var data = _context.GLChart1.Find(id);
            if (data == null) return NotFound();

            _context.GLChart1.Remove(data);
            _context.SaveChanges();

            return Ok();
        }

        // DETAIL
        [HttpPost]
        public IActionResult AddDetail(GLChart3 model)
        {
            var account = _context.GLChart1.Find(model.GLChart1Id);

            model.AC1 = account.AC1; // inherit parent
            model.AC3 = GenerateAC3(model.GLChart1Id);
            model.ACC = account.AC1 + GenerateAC3(model.GLChart1Id);
            model.AcType = account.AcType;
            model.CType = account.CType;
            model.CHName = account.Name;
            model.CoCode = User.GetCompanyId().ToString();

            _context.GLChart3.Add(model);
            _context.SaveChanges();

            return Ok();
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
        public IActionResult UpdateDetail(GLChart3 model)
        {
            var data = _context.GLChart3.Find(model.Id);
            if (data == null) return NotFound();

            data.Name = model.Name;

            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        public IActionResult DeleteDetail(int id)
        {
            var data = _context.GLChart3.Find(id);
            if (data == null) return NotFound();

            _context.GLChart3.Remove(data);
            _context.SaveChanges();

            return Ok();
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
