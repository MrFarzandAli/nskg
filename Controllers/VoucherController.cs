//using Humanizer;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Rendering;
//using Microsoft.EntityFrameworkCore;
//using Nskg.Data;
//using Nskg.Extensions;
//using Nskg.Helper;
//using Nskg.Models;
//using Nskg.Models.ViewModels;
//using Nskg.Services;
//using System.ComponentModel.Design;

//namespace Nskg.Controllers
//{
//    public class VoucherController : Controller
//    {
//        private readonly ApplicationDbContext _context;
//        private readonly AccountingService _service;

//        public VoucherController(ApplicationDbContext context, AccountingService service)
//        {
//            _context = context;
//            _service = service; 
//        }

//        // LIST
//        //[Route("Voucher/{type}")]
//        public IActionResult Index(string type)
//        {
//            var data = _context.VoHead
//                .Where(x => x.Votype == type)
//                .ToList();

//            ViewBag.Type = type;
//            return View(data);
//        }

//        // CREATE GET
//        //public IActionResult Create(string type)
//        //{
//        //    LoadDropdowns();

//        //    var Vono = GenerateVoucherNo(type);

//        //    var type = type?.ToUpper();

//        //    ViewBag.VoucherType = type;

//        //    ViewBag.IsCashReceipt = type == "CR";
//        //    ViewBag.IsBankReceipt = type == "BR";
//        //    ViewBag.IsCashPayment = type == "CP";
//        //    ViewBag.IsBankPayment = type == "BP";
//        //    ViewBag.Votype = GetVoucherTitle(type); // ✅ ADD THIS


//        //    return View(new VoucherVM
//        //    {
//        //        Head = new VoHead
//        //        {
//        //            Vodate = DateTime.Now,
//        //            EntryDate = DateTime.Now,
//        //            Vono = Vono,
//        //            Votype = type
//        //        },
//        //        Details = new List<VoDet>()
//        //    });
//        //}

//        public IActionResult Create(string type)
//        {
//            LoadDropdowns();

//            var Vono = GenerateVoucherNo(type);

//            type = type?.ToUpper();

//            ViewBag.VoucherType = type;

//            // Receipt types (CR, BR)
//            ViewBag.IsReceipt = type == "CR" || type == "BR";

//            // Full columns for JV, CP, BP (show everything)
//            ViewBag.IsFull = type == "JV" || type == "CP" || type == "BP";

//            // specific flags
//            ViewBag.IsCashReceipt = type == "CR";
//            ViewBag.IsBankReceipt = type == "BR";
//            ViewBag.IsCashPayment = type == "CP";
//            ViewBag.IsBankPayment = type == "BP";

//            ViewBag.Votype = GetVoucherTitle(type);

//            return View(new VoucherVM
//            {
//                Head = new VoHead
//                {
//                    Vodate = DateTime.Now,
//                    EntryDate = DateTime.Now,
//                    Vono = Vono,
//                    Votype = type
//                },
//                Details = new List<VoDet>()
//            });
//        }
//        // CREATE POST
//        [HttpPost]
//        public IActionResult Create(VoucherVM vm)
//        {
//            var type = vm.Head.Votype; // ✅ dynamic

//            var setting = _context.VoucherTypeSettings
//                .FirstOrDefault(x => x.Code == type);

//            if (setting.AutoBalance)
//            {

//                var dr = vm.Details.Sum(x => x.Dramt ?? 0);
//                var cr = vm.Details.Sum(x => x.Cramt ?? 0);


//                //if (dr != cr)
//                //{
//                //    ModelState.AddModelError("", "Debit & Credit not equal");
//                //    LoadDropdowns();
//                //    return View(vm);
//                //}
//            }
//            var account = _context.GLChart3.FirstOrDefault(x => x.Id == vm.Head.gl3Id);
//            vm.Head.Vono = GenerateVoucherNo(type);
//            vm.Head.Totdramt = vm.Details.Sum(x => x.Dramt ?? 0);
//            vm.Head.Totcramt = vm.Details.Sum(x => x.Cramt ?? 0);
//            vm.Head.Diff = vm.Head.Totdramt - vm.Head.Totcramt;

//            vm.Head.Hdramt = vm.Head.Diff < 0 ? vm.Head.Diff : 0;
//            vm.Head.Hcramt = vm.Head.Diff > 0 ? -vm.Head.Diff : 0;
//            vm.Head.Entries = vm.Details.Count;
//            vm.Head.CompanyId = User.GetCompanyId();
//            vm.Head.FinancialYearId = User.GetFinancialYearId();
//            vm.Head.Cocode = User.GetCompanyCode();
//            vm.Head.Userid = User.GetUserId();
//            vm.Head.Ac1 = account.AC1;
//            vm.Head.Ac3 = account.AC3;
//            vm.Head.Haccode = account.ACC;

//            _context.VoHead.Add(vm.Head);
//            _context.SaveChanges();

//            vm.Details = vm.Details
//    .Where(x => (x.Dramt ?? 0) != 0 || (x.Cramt ?? 0) != 0)
//    .ToList();

//            foreach (var d in vm.Details)
//            {
//                var DetailAccount = _context.GLChart3.FirstOrDefault(x => x.Id == d.gl3Id);
//                d.VoHeadId = vm.Head.Id;
//                d.Vono = vm.Head.Vono;
//                d.Votype = vm.Head.Votype;
//                d.Vodate = vm.Head.Vodate;
//                d.Ac1 = DetailAccount.AC1;
//                d.Ac3 = DetailAccount.AC3;
//                d.Hacc = account.ACC;
//                d.Acc  = DetailAccount.ACC;
//                d.Actype = DetailAccount.AcType;
//                d.Cocode = User.GetCompanyCode();
//                d.Name = DetailAccount.Name;
//                d.Ctype = DetailAccount.CType;
//                d.EntryDate = vm.Head.EntryDate;
//                d.Userid = User.GetUserId();

//                _context.VoDet.Add(d);
//            }

//            _context.SaveChanges();
//                _service.PostVoucher(vm.Head, vm.Details);


//            return RedirectToAction("Index", new { type = type });
//        }

//        // EDIT GET
//        //public IActionResult Edit(int id)
//        //{
//        //    var data = _context.VoHead
//        //        .Include(x => x.Details)
//        //        .FirstOrDefault(x => x.Id == id);

//        //    ViewBag.Type = data.Votype; // ✅ keep type

//        //    return View(new VoucherVM
//        //    {
//        //        Head = data,
//        //        Details = data.Details.ToList()
//        //    });
//        //}
//        // EDIT GET
//        public IActionResult Edit(int id)
//        {
//            var data = _context.VoHead
//                .Include(x => x.Details)
//                .FirstOrDefault(x => x.Id == id);

//            if (data == null) return NotFound();

//            // Load all dropdowns same as Create
//            LoadDropdowns();

//            var type = data.Votype?.ToUpper();

//            ViewBag.VoucherType = type;
//            ViewBag.Type = type; // Keep for backward compatibility if needed

//            // Receipt types (CR, BR)
//            ViewBag.IsReceipt = type == "CR" || type == "BR";

//            // Full columns for JV, CP, BP (show everything)
//            ViewBag.IsFull = type == "JV" || type == "CP" || type == "BP";

//            // specific flags
//            ViewBag.IsCashReceipt = type == "CR";
//            ViewBag.IsBankReceipt = type == "BR";
//            ViewBag.IsCashPayment = type == "CP";
//            ViewBag.IsBankPayment = type == "BP";

//            ViewBag.Votype = GetVoucherTitle(type);

//            return View(new VoucherVM
//            {
//                Head = data,
//                Details = data.Details.ToList()
//            });
//        }

//        // EDIT POST
//        [HttpPost]
//        public IActionResult Edit(VoucherVM vm)
//        {
//            var existing = _context.VoHead
//                .Include(x => x.Details)
//                .FirstOrDefault(x => x.Id == vm.Head.Id);

//            _context.VoDet.RemoveRange(existing.Details);

//            foreach (var d in vm.Details)
//            {
//                d.VoHeadId = existing.Id;
//                _context.VoDet.Add(d);
//            }

//            existing.Narration = vm.Head.Narration;
//            existing.Vodate = vm.Head.Vodate;

//            _context.SaveChanges();
//            // AFTER SaveChanges in Edit POST
//            _service.PostVoucher(existing, vm.Details);

//            return RedirectToAction("Index");
//        }

//        // DELETE
//        public IActionResult Delete(int id, string type)
//        {
//            var v = _context.VoHead.Find(id);

//            if (v == null) return NotFound();

//            var gl = _context.GLTrans.Where(x => x.Vono == v.Vono);
//            _context.GLTrans.RemoveRange(gl);

//            _context.VoHead.Remove(v);
//            _context.SaveChanges();

//            return RedirectToAction("Index", new { type = type });
//        }

//        // AUTO VOUCHER NO
//        private string GenerateVoucherNo(string type)
//        {
//            var last = _context.VoHead
//                .Where(x => x.Votype == type)
//                .OrderByDescending(x => x.Id)
//                .FirstOrDefault();

//            int next = last == null ? 1 :
//                int.Parse(last.Vono.Split('/').Last()) + 1;

//            return $"{type}/{DateTime.Now.Year}/{next:0000}";
//        }

//        private string GetVoucherTitle(string type)
//        {
//            return type switch
//            {
//                "JV" => "Journal Voucher",
//                "CR" => "Cash Receipt Voucher",
//                "BR" => "Bank Receipt Voucher",
//                "CP" => "Cash Payment Voucher",
//                "BP" => "Bank Payment Voucher",
//                _ => "Voucher Entry"
//            };
//        }

//        private void LoadDropdowns()
//        {
//            ViewBag.Types = _context.VoucherTypeSettings.ToList();

//            var partylist = _context.AcPara
//     .Where(a => AccountCategories.Party.Contains(a.ActypeCode)
//                 && a.Cocode == User.GetCompanyId().ToString()
//                 && a.Parent == "P")
//     .Select(a => a.Accode)
//     .Distinct();

//            ViewBag.PartyAccounts = _context.GLChart3
//               .Where(g =>
//                   g.CoCode == User.GetCompanyId().ToString() &&
//                   g.AcType != "S" &&
//                   partylist.Contains(g.AC1)
//               )
//               .Select(g => new SelectListItem
//               {
//                   Value = g.Id.ToString(),
//                   Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
//               })
//               .OrderBy(x => x.Text)
//               .ToList();

//            var transporterList = _context.AcPara
//     .Where(a => AccountCategories.Transporter.Contains(a.ActypeCode)
//                 && a.Cocode == User.GetCompanyId().ToString()
//                 && a.Parent == "P")
//     .Select(a => a.Accode)
//     .Distinct();

//            ViewBag.transporterList = _context.GLChart3
//               .Where(g =>
//                   g.CoCode == User.GetCompanyId().ToString() &&
//                   g.AcType != "S" &&
//                   transporterList.Contains(g.AC1)
//               )
//               .Select(g => new SelectListItem
//               {
//                   Value = g.Id.ToString(),
//                   Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
//               })
//               .OrderBy(x => x.Text)
//               .ToList();


//            var ac1List = _context.AcPara
//     .Where(a => AccountCategories.Bank.Contains(a.ActypeCode) && a.Cocode == User.GetCompanyId().ToString() && a.Parent == "P")
//     .Select(a => a.Accode)
//     .Distinct();



//            ViewBag.HeaderAccounts = _context.GLChart3
//                .Where(g =>
//                    g.CoCode == User.GetCompanyId().ToString() &&
//                    g.AcType != "S" &&
//                    ac1List.Contains(g.AC1)
//                )
//                .Select(g => new SelectListItem
//                {
//                    Value = g.Id.ToString(),
//                    Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
//                })
//                .OrderBy(x => x.Text)
//                .ToList();

//            ViewBag.Accounts = _context.GLChart3
//                .Where(g =>
//                    g.CoCode == User.GetCompanyId().ToString() &&
//                    g.AcType != "S"                    
//                )
//                .Select(g => new SelectListItem
//                {
//                    Value = g.Id.ToString(),
//                    Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
//                })
//                .OrderBy(x => x.Text)
//                .ToList();
//        }
//    }
//}
using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Extensions;
using Nskg.Helper;
using Nskg.Models;
using Nskg.Models.ViewModels;
using Nskg.Repositories.Interfaces;
using Nskg.Services;
using System.ComponentModel.Design;

namespace Nskg.Controllers
{
    [Authorize] // Add authorization if needed
    public class VoucherController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AccountingService _service;
        private readonly IAuditService _audit; // ✅ ADD AUDIT SERVICE

        public VoucherController(ApplicationDbContext context, AccountingService service, IAuditService audit)
        {
            _context = context;
            _service = service;
            _audit = audit; // ✅ ADD
        }

        // LIST
        public IActionResult Index(string type)
        {
            try
            {
                var data = _context.VoHead
                    .Where(x => x.Votype == type)
                    .ToList();

                ViewBag.Type = type;
                ViewBag.PageTitle = GetVoucherTitle(type) + " List"; // ✅ ADD THIS

                return View(data);
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "Voucher", "0", ex.Message).Wait();
                TempData["ErrorMessage"] = "❌ Failed to load vouchers!";
                return View(new List<VoHead>());
            }
        }

        // CREATE GET
        public IActionResult Create(string type)
        {
            try
            {
                LoadDropdowns();

                var Vono = GenerateVoucherNo(type);

                type = type?.ToUpper();

                ViewBag.VoucherType = type;

                // Receipt types (CR, BR)
                ViewBag.IsReceipt = type == "CR" || type == "BR";

                // Full columns for JV, CP, BP (show everything)
                ViewBag.IsFull = type == "JV" || type == "CP" || type == "BP";

                // specific flags
                ViewBag.IsCashReceipt = type == "CR";
                ViewBag.IsBankReceipt = type == "BR";
                ViewBag.IsCashPayment = type == "CP";
                ViewBag.IsBankPayment = type == "BP";

                ViewBag.Votype = GetVoucherTitle(type);

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
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "Voucher", "0", ex.Message).Wait();
                TempData["ErrorMessage"] = "❌ Failed to load create form!";
                return RedirectToAction("Index", new { type = type });
            }
        }

        // CREATE POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(VoucherVM vm)
        {
            try
            {
                var type = vm.Head.Votype;

                var setting = _context.VoucherTypeSettings
                    .FirstOrDefault(x => x.Code == type);

                if (setting != null && setting.AutoBalance)
                {
                    var dr = vm.Details.Sum(x => x.Dramt ?? 0);
                    var cr = vm.Details.Sum(x => x.Cramt ?? 0);

                    if (dr != cr)
                    {
                        TempData["ErrorMessage"] = "❌ Debit and Credit amounts must be equal!";
                        LoadDropdowns();
                        return View(vm);
                    }
                }

                var account = _context.GLChart3.FirstOrDefault(x => x.Id == vm.Head.gl3Id);
                if (account == null)
                {
                    TempData["ErrorMessage"] = "❌ Invalid account selected!";
                    LoadDropdowns();
                    return View(vm);
                }

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
                    if (DetailAccount != null)
                    {
                        d.VoHeadId = vm.Head.Id;
                        d.Vono = vm.Head.Vono;
                        d.Votype = vm.Head.Votype;
                        d.Vodate = vm.Head.Vodate;
                        d.Ac1 = DetailAccount.AC1;
                        d.Ac3 = DetailAccount.AC3;
                        d.Hacc = account.ACC;
                        d.Acc = DetailAccount.ACC;
                        d.Actype = DetailAccount.AcType;
                        d.Cocode = User.GetCompanyCode();
                        d.Name = DetailAccount.Name;
                        d.Ctype = DetailAccount.CType;
                        d.EntryDate = vm.Head.EntryDate;
                        d.Userid = User.GetUserId();

                        _context.VoDet.Add(d);
                    }
                }

                _context.SaveChanges();
                _service.PostVoucher(vm.Head, vm.Details);

                // 🔥 AUDIT LOG
                _audit.LogAsync(
                    "Create",
                    "Voucher",
                    vm.Head.Id.ToString(),
                    $"Created {GetVoucherTitle(type)}: {vm.Head.Vono} | Amount: {vm.Head.Totdramt}"
                ).Wait();

                TempData["SuccessMessage"] = $"✨ {GetVoucherTitle(type)} created successfully!";
                return RedirectToAction("Index", new { type = type });
            }
            catch (Exception ex)
            {
                // 🔥 ERROR LOG
                _audit.LogAsync(
                    "Error",
                    "Voucher",
                    "0",
                    ex.Message
                ).Wait();

                TempData["ErrorMessage"] = "❌ Failed to create voucher!";
                LoadDropdowns();
                return View(vm);
            }
        }

        // EDIT GET
        public IActionResult Edit(int id)
        {
            try
            {
                var data = _context.VoHead
                    .Include(x => x.Details)
                    .FirstOrDefault(x => x.Id == id);

                if (data == null)
                {
                    TempData["ErrorMessage"] = "❌ Voucher not found!";
                    return RedirectToAction("Index", new { type = "" });
                }

                // Load all dropdowns same as Create
                LoadDropdowns();

                var type = data.Votype?.ToUpper();

                ViewBag.VoucherType = type;
                ViewBag.Type = type;

                // Receipt types (CR, BR)
                ViewBag.IsReceipt = type == "CR" || type == "BR";

                // Full columns for JV, CP, BP (show everything)
                ViewBag.IsFull = type == "JV" || type == "CP" || type == "BP";

                // specific flags
                ViewBag.IsCashReceipt = type == "CR";
                ViewBag.IsBankReceipt = type == "BR";
                ViewBag.IsCashPayment = type == "CP";
                ViewBag.IsBankPayment = type == "BP";

                ViewBag.Votype = GetVoucherTitle(type);

                return View(new VoucherVM
                {
                    Head = data,
                    Details = data.Details.ToList()
                });
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "Voucher", id.ToString(), ex.Message).Wait();
                TempData["ErrorMessage"] = "❌ Failed to load voucher for editing!";
                return RedirectToAction("Index", new { type = "" });
            }
        }

        // EDIT POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(VoucherVM vm)
        {
            try
            {
                var existing = _context.VoHead
                    .Include(x => x.Details)
                    .FirstOrDefault(x => x.Id == vm.Head.Id);

                if (existing == null)
                {
                    TempData["ErrorMessage"] = "❌ Voucher not found!";
                    return RedirectToAction("Index", new { type = "" });
                }

                var oldVono = existing.Vono;
                var oldType = existing.Votype;

                _context.VoDet.RemoveRange(existing.Details);

                foreach (var d in vm.Details)
                {
                    d.VoHeadId = existing.Id;
                    _context.VoDet.Add(d);
                }

                existing.Narration = vm.Head.Narration;
                existing.Vodate = vm.Head.Vodate;
                existing.PersonName = vm.Head.PersonName;
                existing.Invno = vm.Head.Invno;
                existing.gl3Id = vm.Head.gl3Id;
                existing.Partycode = vm.Head.Partycode;

                _context.SaveChanges();

                // Update GL Transactions
                _service.PostVoucher(existing, vm.Details);

                // 🔥 AUDIT LOG
                _audit.LogAsync(
                    "Update",
                    "Voucher",
                    existing.Id.ToString(),
                    $"Updated {GetVoucherTitle(oldType)}: {oldVono} | New Narration: {existing.Narration}"
                ).Wait();

                TempData["InfoMessage"] = $"✏️ {GetVoucherTitle(oldType)} updated successfully!";
                return RedirectToAction("Index", new { type = existing.Votype });
            }
            catch (Exception ex)
            {
                // 🔥 ERROR LOG
                _audit.LogAsync(
                    "Error",
                    "Voucher",
                    vm.Head.Id.ToString(),
                    ex.Message
                ).Wait();

                TempData["ErrorMessage"] = "❌ Failed to update voucher!";
                LoadDropdowns();
                return View(vm);
            }
        }

        // DELETE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id, string type)
        {
            try
            {
                var v = _context.VoHead.Find(id);

                if (v == null)
                {
                    return Json(new { success = false, message = "Voucher not found!" });
                }

                var gl = _context.GLTrans.Where(x => x.Vono == v.Vono);
                _context.GLTrans.RemoveRange(gl);

                _context.VoHead.Remove(v);
                _context.SaveChanges();

                // 🔥 AUDIT LOG
                _audit.LogAsync(
                    "Delete",
                    "Voucher",
                    id.ToString(),
                    $"Deleted {GetVoucherTitle(v.Votype)}: {v.Vono}"
                ).Wait();

                return Json(new { success = true, message = "Voucher deleted successfully!" });
            }
            catch (Exception ex)
            {
                // 🔥 ERROR LOG
                _audit.LogAsync(
                    "Error",
                    "Voucher",
                    id.ToString(),
                    ex.Message
                ).Wait();

                return Json(new { success = false, message = ex.Message });
            }
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

        private string GetVoucherTitle(string type)
        {
            return type switch
            {
                "JV" => "Journal Voucher",
                "CR" => "Cash Receipt Voucher",
                "BR" => "Bank Receipt Voucher",
                "CP" => "Cash Payment Voucher",
                "BP" => "Bank Payment Voucher",
                _ => "Voucher Entry"
            };
        }

        private void LoadDropdowns()
        {
            try
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
                    .Where(a => AccountCategories.Bank.Contains(a.ActypeCode)
                                && a.Cocode == User.GetCompanyId().ToString()
                                && a.Parent == "P")
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
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "LoadDropdowns", "0", ex.Message).Wait();
                throw;
            }
        }
    }
}
