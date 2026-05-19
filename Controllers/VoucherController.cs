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
        private string GetUser()
        {
            return User?.Identity?.Name ?? "System";
        }

        // LIST
        public IActionResult Index(string type)
        {
            try
            {
                //var data = _context.VoHead
                //    .Where(x => x.Votype == type)
                //    .ToList();
                var data = _context.VoHead
    .Where(x => x.Votype == type && !x.IsDeleted)
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
                LoadDropdowns(type);

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
            var type = vm.Head.Votype;
            try
            {          

                var setting = _context.VoucherTypeSettings
                    .FirstOrDefault(x => x.Code == type);

                if (setting != null && setting.AutoBalance)
                {
                    var dr = vm.Details.Sum(x => x.Dramt ?? 0);
                    var cr = vm.Details.Sum(x => x.Cramt ?? 0);

                    if (dr != cr && type == "JV")
                    {
                        TempData["ErrorMessage"] = "❌ Debit and Credit amounts must be equal!";
                        LoadDropdowns(type);
                        return View(vm);
                    }
                }

                var account = _context.GLChart3.FirstOrDefault(x => x.Id == vm.Head.gl3Id);
                if (account == null && type != "JV")
                {
                    TempData["ErrorMessage"] = "❌ Invalid account selected!";
                    LoadDropdowns(type);
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
                vm.Head.Ac1 = account?.AC1;
                vm.Head.Ac3 = account?.AC3;
                vm.Head.Haccode = account?.ACC;
                // 🔥 SET AUDIT FIELDS
                vm.Head.CreatedOn = DateTime.Now;
                vm.Head.CreatedBy = GetUser();
                vm.Head.IsDeleted = false;
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
                        d.Hacc = account?.ACC;
                        d.Acc = DetailAccount.ACC;
                        d.Actype = DetailAccount.AcType;
                        d.Cocode = User.GetCompanyCode();
                        d.Name = DetailAccount.Name;
                        d.Ctype = DetailAccount.CType;
                        d.EntryDate = vm.Head.EntryDate;
                        d.Userid = User.GetUserId();
                        d.CreatedOn = DateTime.Now;
                        d.CreatedBy = GetUser();
                        d.IsDeleted = false;
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
                LoadDropdowns(type);
                return View(vm);
            }
        }

        // EDIT GET
        //public IActionResult Edit(int id)
        //{
        //    try
        //    {
        //        var data = _context.VoHead
        //            .Include(x => x.Details)
        //            .FirstOrDefault(x => x.Id == id);

        //        if (data == null)
        //        {
        //            TempData["ErrorMessage"] = "❌ Voucher not found!";
        //            return RedirectToAction("Index", new { type = "" });
        //        }



        //        var type = data.Votype?.ToUpper();

        //        ViewBag.VoucherType = type;
        //        ViewBag.Type = type;

        //        // Load all dropdowns same as Create
        //        LoadDropdowns(type);


        //        // Receipt types (CR, BR)
        //        ViewBag.IsReceipt = type == "CR" || type == "BR";

        //        // Full columns for JV, CP, BP (show everything)
        //        ViewBag.IsFull = type == "JV" || type == "CP" || type == "BP";

        //        // specific flags
        //        ViewBag.IsCashReceipt = type == "CR";
        //        ViewBag.IsBankReceipt = type == "BR";
        //        ViewBag.IsCashPayment = type == "CP";
        //        ViewBag.IsBankPayment = type == "BP";

        //        ViewBag.Votype = GetVoucherTitle(type);

        //        return View(new VoucherVM
        //        {
        //            Head = data,
        //            Details = data.Details.ToList()
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _audit.LogAsync("Error", "Voucher", id.ToString(), ex.Message).Wait();
        //        TempData["ErrorMessage"] = "❌ Failed to load voucher for editing!";
        //        return RedirectToAction("Index", new { type = "" });
        //    }
        //}
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
                LoadDropdowns(data.Votype);

                var type = data.Votype?.ToUpper();

                ViewBag.VoucherType = type;
                ViewBag.Type = type;

                // ✅ SET ALL FLAGS (same as Create action)
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

            try
            {
                var setting = _context.VoucherTypeSettings
                   .FirstOrDefault(x => x.Code == oldType);

                if (setting != null && setting.AutoBalance)
                {
                    var dr = vm.Details.Sum(x => x.Dramt ?? 0);
                    var cr = vm.Details.Sum(x => x.Cramt ?? 0);

                    if (dr != cr && oldType == "JV")
                    {
                        TempData["ErrorMessage"] = "❌ Debit and Credit amounts must be equal!";
                        LoadDropdowns(oldType);
                        return View(vm);
                    }
                }


                // =========================================
                // 🔥 STEP 1: REMOVE OLD GL TRANSACTIONS
                // =========================================
                var oldGl = _context.GLTrans
                    .Where(x => x.RefId == existing.Id && x.Votype == oldType)
                    .ToList();

                if (oldGl.Any())
                {
                    _context.GLTrans.RemoveRange(oldGl);
                }

                var account = _context.GLChart3.FirstOrDefault(x => x.Id == vm.Head.gl3Id);
              
                if (account == null && oldType != "JV")
                {
                    TempData["ErrorMessage"] = "❌ Invalid account selected!";
                    LoadDropdowns(oldType);
                    return View(vm);
                }
                // =========================================
                // 🔥 STEP 2: UPDATE VOUCHER HEAD
                // =========================================
                existing.Narration = vm.Head.Narration;
                existing.Vodate = vm.Head.Vodate;
                existing.PersonName = vm.Head.PersonName;
                existing.Invno = vm.Head.Invno;
                existing.gl3Id = vm.Head.gl3Id;
                existing.Partycode = vm.Head.Partycode;
                existing.Totdramt = vm.Details.Sum(x => x.Dramt ?? 0);
                existing.Totcramt = vm.Details.Sum(x => x.Cramt ?? 0);
                existing.Diff = vm.Head.Totdramt - vm.Head.Totcramt;

                existing.Hdramt = vm.Head.Diff < 0 ? vm.Head.Diff : 0;
                existing.Hcramt = vm.Head.Diff > 0 ? -vm.Head.Diff : 0;
                existing.Entries = vm.Details.Count;
                existing.CompanyId = User.GetCompanyId();
                existing.FinancialYearId = User.GetFinancialYearId();
                existing.Cocode = User.GetCompanyCode();
                existing.Userid = User.GetUserId();
                existing.Ac1 = account?.AC1;
                existing.Ac3 = account?.AC3;
                existing.Haccode = account?.ACC;

                // =========================================
                // 🔥 STEP 3: REPLACE DETAILS
                // =========================================
                //  _context.VoDet.RemoveRange(existing.Details);
                foreach (var d in existing.Details)
                {
                    d.IsDeleted = true;
                    d.ModifiedOn = DateTime.Now;
                    d.ModifiedBy = GetUser();

                    _context.VoDet.Update(d);
                }

                foreach (var d in vm.Details)
                {
                    var DetailAccount = _context.GLChart3.FirstOrDefault(x => x.Id == d.gl3Id);
                    if (DetailAccount != null)
                    {
                        d.VoHeadId = existing.Id;
                        d.Vono = vm.Head.Vono;
                        d.Votype = vm.Head.Votype;
                        d.Vodate = vm.Head.Vodate;
                        d.Ac1 = DetailAccount.AC1;
                        d.Ac3 = DetailAccount.AC3;
                        d.Hacc = account?.ACC;
                        d.Acc = DetailAccount.ACC;
                        d.Actype = DetailAccount.AcType;
                        d.Cocode = User.GetCompanyCode();
                        d.Name = DetailAccount.Name;
                        d.Ctype = DetailAccount.CType;
                        d.EntryDate = vm.Head.EntryDate;
                        d.Userid = User.GetUserId();
                        // 🔥 ADD THIS
                        d.CreatedOn = DateTime.Now;   // new record hai
                        d.CreatedBy = GetUser();
                        d.IsDeleted = false;
                        _context.VoDet.Add(d);
                    }
                }
                // 🔥 AUDIT UPDATE
                existing.ModifiedOn = DateTime.Now;
                existing.ModifiedBy = GetUser();
                _context.SaveChanges();

                // =========================================
                // 🔥 STEP 4: REPOST GL TRANSACTIONS
                // =========================================
                _service.PostVoucher(existing, vm.Details);

                // =========================================
                // 🔥 AUDIT LOG
                // =========================================
                _audit.LogAsync(
                    "Update",
                    "Voucher",
                    existing.Id.ToString(),
                    $"Updated {GetVoucherTitle(oldType)}: {oldVono}"
                ).Wait();

                TempData["InfoMessage"] = $"✏️ {GetVoucherTitle(oldType)} updated successfully!";
                return RedirectToAction("Index", new { type = existing.Votype });
            }
            catch (Exception ex)
            {
                _audit.LogAsync(
                    "Error",
                    "Voucher",
                    vm.Head.Id.ToString(),
                    ex.Message
                ).Wait();

                TempData["ErrorMessage"] = "❌ Failed to update voucher!";
                LoadDropdowns(oldType);
                return View(vm);
            }
        }

        // DELETE
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult Delete(int id)
        //{
        //    try
        //    {
        //        var v = _context.VoHead
        //            .Include(x => x.Details)
        //            .FirstOrDefault(x => x.Id == id);

        //        if (v == null)
        //        {
        //            return Json(new { success = false, message = "Voucher not found!" });
        //        }

        //        var gl = _context.GLTrans
        //            .Where(x => x.RefId == v.Id);

        //        _context.GLTrans.RemoveRange(gl);

        //        _context.VoDet.RemoveRange(v.Details);
        //        _context.VoHead.Remove(v);

        //        _context.SaveChanges();

        //        _audit.LogAsync("Delete", "Voucher", id.ToString(),
        //            $"Deleted {v.Votype}: {v.Vono}").Wait();

        //        return Json(new { success = true, message = "Voucher deleted successfully!" });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { success = false, message = ex.Message });
        //    }
        //}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            try
            {
                var v = _context.VoHead
                    .Include(x => x.Details)
                    .FirstOrDefault(x => x.Id == id);

                if (v == null)
                {
                    return Json(new { success = false, message = "Voucher not found!" });
                }

                // 🔥 GL REMOVE (same as before)
                var gl = _context.GLTrans
                    .Where(x => x.RefId == v.Id);

                _context.GLTrans.RemoveRange(gl);

                // 🔥 SOFT DELETE DETAILS (INSTEAD OF RemoveRange)
                foreach (var d in v.Details)
                {
                    d.IsDeleted = true;
                    d.ModifiedOn = DateTime.Now;
                    d.ModifiedBy = GetUser();

                    _context.VoDet.Update(d);
                }

                // 🔥 SOFT DELETE HEAD
                v.IsDeleted = true;
                v.ModifiedOn = DateTime.Now;
                v.ModifiedBy = GetUser();

                _context.VoHead.Update(v);

                _context.SaveChanges();

                _audit.LogAsync("Delete", "Voucher", id.ToString(),
                    $"Soft Deleted {v.Votype}: {v.Vono}").Wait();

                return Json(new { success = true, message = "Voucher deleted successfully!" });
            }
            catch (Exception ex)
            {
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

        private void LoadDropdowns(string votype)
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

                var partyAccounts = _context.GLChart3
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

                // ✅ Add default item at index 0
                partyAccounts.Insert(0, new SelectListItem
                {
                    Value = "",
                    Text = "-- Select Party Account --"
                });

                ViewBag.PartyAccounts = partyAccounts;

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

          

                IEnumerable<string> allowedTypes = votype switch
                {
                    "BP" or "BR" => AccountCategories.Bank,
                    "CP" or "CR" => AccountCategories.Cash,
                    _ => Array.Empty<string>()
                };

                var ac1List = _context.AcPara
                    .Where(a =>
                        allowedTypes.Contains(a.ActypeCode)
                        && a.Cocode == User.GetCompanyId().ToString()
                        && a.Parent == "P"
                    )
                    .Select(a => a.Accode)
                    .Distinct()
                    .ToList();



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
