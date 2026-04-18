using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Extensions;
using Nskg.Helper;
using Nskg.Models;
using Nskg.Models.ViewModels;
using Nskg.Services;
using System.ComponentModel.Design;

namespace Nskg.Controllers
{
    public class VoucherController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AccountingService _service;

        public VoucherController(ApplicationDbContext context, AccountingService service)
        {
            _context = context;
            _service = service; 
        }

        // LIST
        //[Route("Voucher/{type}")]
        public IActionResult Index(string type)
        {
            var data = _context.VoHead
                .Where(x => x.Votype == type)
                .ToList();

            ViewBag.Type = type;
            return View(data);
        }

        // CREATE GET
        public IActionResult Create(string type)
        {
            LoadDropdowns();

            var Vono = GenerateVoucherNo(type);

            return View(new VoucherVM
            {
                Head = new VoHead
                {
                    Vodate = DateTime.Now,
                    EntryDate = DateTime.Now,
                    Vono = Vono,
                    Votype = type
                },
                Details = new List<VoDet>()
            });
        }

        // CREATE POST
        [HttpPost]
        public IActionResult Create(VoucherVM vm)
        {
            var type = vm.Head.Votype; // ✅ dynamic

            var setting = _context.VoucherTypeSettings
                .FirstOrDefault(x => x.Code == type);

            if (setting.AutoBalance)
            {

                var dr = vm.Details.Sum(x => x.Dramt ?? 0);
                var cr = vm.Details.Sum(x => x.Cramt ?? 0);
               

                //if (dr != cr)
                //{
                //    ModelState.AddModelError("", "Debit & Credit not equal");
                //    LoadDropdowns();
                //    return View(vm);
                //}
            }
            var account = _context.GLChart3.FirstOrDefault(x => x.Id == vm.Head.gl3Id);
            vm.Head.Vono = GenerateVoucherNo(type);
            vm.Head.Totdramt = vm.Details.Sum(x => x.Dramt ?? 0);
            vm.Head.Totcramt = vm.Details.Sum(x => x.Cramt ?? 0);
            vm.Head.Diff = vm.Head.Totdramt - vm.Head.Totcramt;

            vm.Head.Hdramt = vm.Head.Diff < 0 ? vm.Head.Diff : 0;
            vm.Head.Hcramt = vm.Head.Diff > 0 ? -vm.Head.Diff : 0;
            vm.Head.Entries = vm.Details.Count;
            vm.Head.CompanyId = User.GetCompanyId();
            vm.Head.FinancialYearId = User.GetFinancialYearId();
            vm.Head.Cocode = User.GetCompanyCode();
            vm.Head.Userid = User.GetUserId();
            vm.Head.Ac1 = account.AC1;
            vm.Head.Ac3 = account.AC3;
            vm.Head.Haccode = account.ACC;

            _context.VoHead.Add(vm.Head);
            _context.SaveChanges();

            vm.Details = vm.Details
    .Where(x => (x.Dramt ?? 0) != 0 || (x.Cramt ?? 0) != 0)
    .ToList();

            foreach (var d in vm.Details)
            {
                var DetailAccount = _context.GLChart3.FirstOrDefault(x => x.Id == d.gl3Id);
                d.VoHeadId = vm.Head.Id;
                d.Vono = vm.Head.Vono;
                d.Votype = vm.Head.Votype;
                d.Vodate = vm.Head.Vodate;
                d.Ac1 = DetailAccount.AC1;
                d.Ac3 = DetailAccount.AC3;
                d.Hacc = account.ACC;
                d.Acc  = DetailAccount.ACC;
                d.Actype = DetailAccount.AcType;
                d.Cocode = User.GetCompanyCode();
                d.Name = DetailAccount.Name;
                d.Ctype = DetailAccount.CType;
                d.EntryDate = vm.Head.EntryDate;
                d.Userid = User.GetUserId();

                _context.VoDet.Add(d);
            }

            _context.SaveChanges();
                _service.PostVoucher(vm.Head, vm.Details);


            return RedirectToAction("Index", new { type = type });
        }

        // EDIT GET
        public IActionResult Edit(int id)
        {
            var data = _context.VoHead
                .Include(x => x.Details)
                .FirstOrDefault(x => x.Id == id);

            ViewBag.Type = data.Votype; // ✅ keep type

            return View(new VoucherVM
            {
                Head = data,
                Details = data.Details.ToList()
            });
        }

        // EDIT POST
        [HttpPost]
        public IActionResult Edit(VoucherVM vm)
        {
            var existing = _context.VoHead
                .Include(x => x.Details)
                .FirstOrDefault(x => x.Id == vm.Head.Id);

            _context.VoDet.RemoveRange(existing.Details);

            foreach (var d in vm.Details)
            {
                d.VoHeadId = existing.Id;
                _context.VoDet.Add(d);
            }

            existing.Narration = vm.Head.Narration;
            existing.Vodate = vm.Head.Vodate;

            _context.SaveChanges();
            // AFTER SaveChanges in Edit POST
            _service.PostVoucher(existing, vm.Details);

            return RedirectToAction("Index");
        }

        // DELETE
        public IActionResult Delete(int id, string type)
        {
            var v = _context.VoHead.Find(id);

            if (v == null) return NotFound();

            var gl = _context.GLTrans.Where(x => x.Vono == v.Vono);
            _context.GLTrans.RemoveRange(gl);

            _context.VoHead.Remove(v);
            _context.SaveChanges();

            return RedirectToAction("Index", new { type = type });
        }

        // AUTO VOUCHER NO
        private string GenerateVoucherNo(string type)
        {
            var last = _context.VoHead
                .Where(x => x.Votype == type)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            int next = last == null ? 1 :
                int.Parse(last.Vono.Split('/').Last()) + 1;

            return $"{type}/{DateTime.Now.Year}/{next:0000}";
        }

        private void LoadDropdowns()
        {
            ViewBag.Types = _context.VoucherTypeSettings.ToList();

            var partylist = _context.AcPara
     .Where(a => AccountCategories.Party.Contains(a.ActypeCode)
                 && a.Cocode == User.GetCompanyId().ToString()
                 && a.Parent == "P")
     .Select(a => a.Accode)
     .Distinct();

            ViewBag.PartyAccounts = _context.GLChart3
               .Where(g =>
                   g.CoCode == User.GetCompanyId().ToString() &&
                   g.AcType != "S" &&
                   partylist.Contains(g.AC1)
               )
               .Select(g => new SelectListItem
               {
                   Value = g.Id.ToString(),
                   Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
               })
               .OrderBy(x => x.Text)
               .ToList();

            var transporterList = _context.AcPara
     .Where(a => AccountCategories.Transporter.Contains(a.ActypeCode)
                 && a.Cocode == User.GetCompanyId().ToString()
                 && a.Parent == "P")
     .Select(a => a.Accode)
     .Distinct();

            ViewBag.transporterList = _context.GLChart3
               .Where(g =>
                   g.CoCode == User.GetCompanyId().ToString() &&
                   g.AcType != "S" &&
                   transporterList.Contains(g.AC1)
               )
               .Select(g => new SelectListItem
               {
                   Value = g.Id.ToString(),
                   Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
               })
               .OrderBy(x => x.Text)
               .ToList();


            var ac1List = _context.AcPara
     .Where(a => AccountCategories.Bank.Contains(a.ActypeCode) && a.Cocode == User.GetCompanyId().ToString() && a.Parent == "P")
     .Select(a => a.Accode)
     .Distinct();

           

            ViewBag.HeaderAccounts = _context.GLChart3
                .Where(g =>
                    g.CoCode == User.GetCompanyId().ToString() &&
                    g.AcType != "S" &&
                    ac1List.Contains(g.AC1)
                )
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
                })
                .OrderBy(x => x.Text)
                .ToList();

            ViewBag.Accounts = _context.GLChart3
                .Where(g =>
                    g.CoCode == User.GetCompanyId().ToString() &&
                    g.AcType != "S"                    
                )
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
                })
                .OrderBy(x => x.Text)
                .ToList();
        }
    }
}
