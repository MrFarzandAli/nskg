using Microsoft.AspNetCore.Mvc;
using Nskg.Data;
using Nskg.Models;
using Nskg.Models.ViewModels;

namespace Nskg.Controllers
{
    public class AcParaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AcParaController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewBag.AccountType = _context.Actype
     .Select(x => new
     {
         x.ACTYPE,
         x.ACNAME
     })
     .ToList();

            return View(_context.AcPara.ToList());
        }

        [HttpPost]
        public IActionResult Create([FromBody] AcPara model)
        {
            // ✅ Duplicate ACCODE validation (still needed)
            if (_context.AcPara.Any(x => x.Accode == model.Accode))
            {
                return BadRequest("Account code already exists!");
            }

            _context.AcPara.Add(model);
            _context.SaveChanges();

            return Ok();
        }

        [HttpPost]
        public IActionResult Edit([FromBody] AcPara model)
        {
            var data = _context.AcPara.Find(model.Id);

            if (data == null) return NotFound();

            data.Accode = model.Accode;
            data.Acname = model.Acname;
            data.Actype = model.Actype;
            data.Parent = model.Parent;
            data.Opening = model.Opening;

            _context.SaveChanges();

            return Ok();
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var data = _context.AcPara.Find(id);

            if (data != null)
            {
                _context.AcPara.Remove(data);
                _context.SaveChanges();
            }

            return Ok();
        }
        public IActionResult GetAccounts(string type)
        {
            if (type == "P")
            {
                var data = _context.GLChart1
                    .Select(x => new
                    {
                        accode = x.AC1,     // ✅ normalize name
                        acname = x.Name
                    })
                    .ToList();

                return Json(data);
            }
            else if (type == "C")
            {
                var data = _context.GLChart3
                    .Select(x => new
                    {
                        accode = x.ACC,     // ✅ normalize name
                        acname = x.Name
                    })
                    .ToList();

                return Json(data);
            }

            return Json(new List<object>());
        }

        public IActionResult Get(int id)
        {
            var data = _context.AcPara.Find(id);
            return Json(data);
        }
    }
}
