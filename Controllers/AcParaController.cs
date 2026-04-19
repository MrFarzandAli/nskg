using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Extensions;
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

            var data = _context.AcPara
                .Include(x => x.Actype)
                .Include(x => x.GLChart1)
                .Include(x => x.GLChart3)
                .ToList();

            return View(data);
        }

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

                var gl = _context.GLChart3.Find(model.GLChart3Id);
                model.Accode = gl?.ACC;
                model.Acname = gl?.Name;

                model.GLChart1Id = null;
            }

            if (_context.AcPara.Any(x => x.Accode == model.Accode))
                return BadRequest("Account code already exists!");

            _context.AcPara.Add(model);
            _context.SaveChanges();

            return Ok();
        }


        [HttpPost]
        public IActionResult Edit([FromBody] AcPara model)
        {
            var data = _context.AcPara.Find(model.Id);
            if (data == null) return NotFound();

            if (_context.AcPara.Any(x => x.Accode == model.Accode && x.Id != model.Id))
                return BadRequest("Account code already exists!");

            // ✅ Actype validation
            if (!string.IsNullOrEmpty(model.ActypeCode) &&
                !_context.Actype.Any(x => x.ACTYPE == model.ActypeCode))
                return BadRequest("Invalid Account Type");

            data.ActypeCode = model.ActypeCode;
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

            return Ok();
        }


        [HttpPost]
        public IActionResult Delete(int id)
        {
            var data = _context.AcPara.Find(id);
            if (data == null) return NotFound();

            var used = _context.VoDet.Any(x => x.Acc == data.Accode);
            if (used)
                return BadRequest("Account used in transactions");

            _context.AcPara.Remove(data);
            _context.SaveChanges();

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
                        acname = x.Name,
                        glId = x.Id
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
