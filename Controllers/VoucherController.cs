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
            ViewBag.Type = type;
            ViewBag.PageTitle = GetVoucherTitle(type) + " List";
            return View();
        }

        [HttpPost]
        public IActionResult GetVoucherList(string type)
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();
                var sortColumnIndex = Request.Form["order[0][column]"].FirstOrDefault();
                var sortColumnDir = Request.Form["order[0][dir]"].FirstOrDefault();

                int pageSize = length != null ? Convert.ToInt32(length) : 10;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                int companyId = User.GetCompanyId();
                string companyCode = User.GetCompanyCode();

                var query = _context.VoHead
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted);

                if (companyId > 0)
                {
                    query = query.Where(x => x.CompanyId == companyId || (!string.IsNullOrEmpty(companyCode) && companyCode != "0" && x.Cocode == companyCode));
                }

                if (!string.IsNullOrWhiteSpace(type))
                {
                    query = query.Where(x => x.Votype == type);
                }

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrWhiteSpace(searchValue))
                {
                    searchValue = searchValue.Trim().ToLower();
                    query = query.Where(x =>
                        (x.Vono != null && x.Vono.ToLower().Contains(searchValue)) ||
                        (x.Votype != null && x.Votype.ToLower().Contains(searchValue)) ||
                        (x.Totdramt != null && x.Totdramt.ToString().Contains(searchValue)) ||
                        (x.Totcramt != null && x.Totcramt.ToString().Contains(searchValue))
                    );
                }

                // Column-wise Searching
                var col0Search = Request.Form["columns[0][search][value]"].FirstOrDefault();
                var col1Search = Request.Form["columns[1][search][value]"].FirstOrDefault();
                var col2Search = Request.Form["columns[2][search][value]"].FirstOrDefault();
                var col3Search = Request.Form["columns[3][search][value]"].FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(col0Search))
                {
                    var val0 = col0Search.Trim().ToLower();
                    query = query.Where(x => x.Vono != null && x.Vono.ToLower().Contains(val0));
                }

                if (!string.IsNullOrWhiteSpace(col1Search))
                {
                    var val1 = col1Search.Trim();
                    if (DateTime.TryParse(val1, out var parsedDate))
                    {
                        var targetDate = parsedDate.Date;
                        query = query.Where(x => x.Vodate.HasValue && x.Vodate.Value.Date == targetDate);
                    }
                    else
                    {
                        query = query.Where(x => x.Vodate.HasValue && x.Vodate.Value.ToString().Contains(val1));
                    }
                }

                if (!string.IsNullOrWhiteSpace(col2Search))
                {
                    var val2 = col2Search.Trim().ToLower();
                    query = query.Where(x => x.Votype != null && x.Votype.ToLower().Contains(val2));
                }

                if (!string.IsNullOrWhiteSpace(col3Search))
                {
                    var val3 = col3Search.Trim();
                    query = query.Where(x =>
                        (x.Totdramt != null && x.Totdramt.ToString().Contains(val3)) ||
                        (x.Totcramt != null && x.Totcramt.ToString().Contains(val3))
                    );
                }

                int filterRecords = query.Count();

                // Sorting
                switch (sortColumnIndex)
                {
                    case "0":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.Vono).ThenByDescending(x => x.Vodate).ThenBy(x => x.Id) : query.OrderByDescending(x => x.Vono).ThenByDescending(x => x.Vodate).ThenByDescending(x => x.Id);
                        break;
                    case "1":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.Vodate).ThenBy(x => x.Vono).ThenBy(x => x.Id) : query.OrderByDescending(x => x.Vodate).ThenByDescending(x => x.Vono).ThenByDescending(x => x.Id);
                        break;
                    case "2":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.Votype).ThenByDescending(x => x.Vodate).ThenByDescending(x => x.Vono) : query.OrderByDescending(x => x.Votype).ThenByDescending(x => x.Vodate).ThenByDescending(x => x.Vono);
                        break;
                    case "3":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => (x.Totdramt ?? x.Totcramt ?? 0)).ThenByDescending(x => x.Vodate).ThenByDescending(x => x.Vono) : query.OrderByDescending(x => (x.Totdramt ?? x.Totcramt ?? 0)).ThenByDescending(x => x.Vodate).ThenByDescending(x => x.Vono);
                        break;
                    default:
                        query = query.OrderByDescending(x => x.Vodate).ThenByDescending(x => x.Vono).ThenByDescending(x => x.Id);
                        break;
                }

                var rawData = query.Skip(skip).Take(pageSize)
                    .Select(v => new
                    {
                        v.Id,
                        v.Vono,
                        v.Vodate,
                        v.Votype,
                        v.Totdramt,
                        v.Totcramt
                    })
                    .ToList();

                var data = rawData.Select(v => new
                {
                    id = v.Id,
                    vono = v.Vono ?? "",
                    vodate = v.Vodate.HasValue ? v.Vodate.Value.ToString("dd-MMM-yyyy") : "",
                    votype = v.Votype ?? "",
                    amount = ((v.Totdramt.HasValue && v.Totdramt.Value > 0 ? v.Totdramt.Value : (v.Totcramt ?? 0))).ToString("N2")
                }).ToList();

                return Json(new
                {
                    draw = draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = filterRecords,
                    data = data
                });
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "Voucher", "0", ex.Message).Wait();
                return Json(new { draw = 0, recordsTotal = 0, recordsFiltered = 0, data = new List<object>() });
            }
        }

        // CREATE GET
        public IActionResult Create(string type)
        {
            try
            {
                type = type?.ToUpper() ?? "JV";
                LoadDropdowns(type);

                var Vono = GenerateVoucherNo(type);

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

                // Fetch previous record for this voucher type to pre-fill default values
                int currentCompId = User.GetCompanyId();
                string currentCoCode = User.GetCompanyCode();

                var prevQuery = _context.VoHead
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.Votype == type);

                if (currentCompId > 0)
                {
                    prevQuery = prevQuery.Where(x => x.CompanyId == currentCompId || (!string.IsNullOrEmpty(currentCoCode) && currentCoCode != "0" && x.Cocode == currentCoCode));
                }

                var prevVoucher = prevQuery.OrderByDescending(x => x.Vodate).ThenByDescending(x => x.Id).FirstOrDefault();
                if (prevVoucher == null)
                {
                    prevVoucher = _context.VoHead.AsNoTracking()
                        .Where(x => !x.IsDeleted && x.Votype == type)
                        .OrderByDescending(x => x.Vodate).ThenByDescending(x => x.Id)
                        .FirstOrDefault();
                }

                return View(new VoucherVM
                {
                    Head = new VoHead
                    {
                        Vodate = prevVoucher?.Vodate ?? DateTime.Now,
                        EntryDate = DateTime.Now,
                        Vono = Vono,
                        Votype = type,
                        gl3Id = prevVoucher?.gl3Id,
                        Partycode = prevVoucher?.Partycode,
                        PersonName = prevVoucher?.PersonName,
                        Narration = prevVoucher?.Narration
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

                if (data.Details != null)
                {
                    data.Details = data.Details.Where(x => !x.IsDeleted).ToList();
                }

                // Resolve Head.gl3Id if missing from Haccode or Ac1+Ac3
                if (data.gl3Id == null || data.gl3Id == 0)
                {
                    string hcode = !string.IsNullOrWhiteSpace(data.Haccode) ? data.Haccode.Trim() : ((data.Ac1 ?? "") + (data.Ac3 ?? "")).Trim();
                    if (!string.IsNullOrEmpty(hcode))
                    {
                        var g3 = _context.GLChart3.FirstOrDefault(g =>
                            (g.CompanyId == data.CompanyId || g.CoCode == data.Cocode || g.CompanyId == 0 || g.CompanyId == null) &&
                            ((g.AC1 + g.AC3) == hcode || g.ACC == hcode))
                            ?? _context.GLChart3.FirstOrDefault(g => ((g.AC1 + g.AC3) == hcode || g.ACC == hcode));
                        if (g3 != null)
                        {
                            data.gl3Id = g3.Id;
                        }
                    }
                }

                // Resolve Partycode if code instead of Id
                if (!string.IsNullOrWhiteSpace(data.Partycode) && !int.TryParse(data.Partycode, out _))
                {
                    var partyG3 = _context.GLChart3.FirstOrDefault(g =>
                        (g.CompanyId == data.CompanyId || g.CoCode == data.Cocode || g.CompanyId == 0 || g.CompanyId == null) &&
                        ((g.AC1 + g.AC3) == data.Partycode || g.ACC == data.Partycode))
                        ?? _context.GLChart3.FirstOrDefault(g => ((g.AC1 + g.AC3) == data.Partycode || g.ACC == data.Partycode));
                    if (partyG3 != null)
                    {
                        data.Partycode = partyG3.Id.ToString();
                    }
                }

                // Resolve Details.gl3Id and Transcode if missing/code
                if (data.Details != null)
                {
                    foreach (var d in data.Details)
                    {
                        if (d.gl3Id == null || d.gl3Id == 0)
                        {
                            string dcode = !string.IsNullOrWhiteSpace(d.Acc) ? d.Acc.Trim() : ((d.Ac1 ?? "") + (d.Ac3 ?? "")).Trim();
                            if (!string.IsNullOrEmpty(dcode))
                            {
                                var g3 = _context.GLChart3.FirstOrDefault(g =>
                                    (g.CompanyId == data.CompanyId || g.CoCode == data.Cocode || g.CompanyId == 0 || g.CompanyId == null) &&
                                    ((g.AC1 + g.AC3) == dcode || g.ACC == dcode))
                                    ?? _context.GLChart3.FirstOrDefault(g => ((g.AC1 + g.AC3) == dcode || g.ACC == dcode));
                                if (g3 != null)
                                {
                                    d.gl3Id = g3.Id;
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(d.Transcode) && !int.TryParse(d.Transcode, out _))
                        {
                            var transG3 = _context.GLChart3.FirstOrDefault(g =>
                                (g.CompanyId == data.CompanyId || g.CoCode == data.Cocode || g.CompanyId == 0 || g.CompanyId == null) &&
                                ((g.AC1 + g.AC3) == d.Transcode || g.ACC == d.Transcode))
                                ?? _context.GLChart3.FirstOrDefault(g => ((g.AC1 + g.AC3) == d.Transcode || g.ACC == d.Transcode));
                            if (transG3 != null)
                            {
                                d.Transcode = transG3.Id.ToString();
                            }
                        }
                    }
                }

                var usedAccountIds = new HashSet<int>();
                if (data.gl3Id.HasValue && data.gl3Id > 0) usedAccountIds.Add(data.gl3Id.Value);
                if (int.TryParse(data.Partycode, out int pid) && pid > 0) usedAccountIds.Add(pid);
                if (data.Details != null)
                {
                    foreach (var d in data.Details)
                    {
                        if (d.gl3Id.HasValue && d.gl3Id > 0) usedAccountIds.Add(d.gl3Id.Value);
                        if (int.TryParse(d.Transcode, out int tid) && tid > 0) usedAccountIds.Add(tid);
                    }
                }

                // Load all dropdowns ensuring all used accounts in this voucher are included
                LoadDropdowns(data.Votype, data.CompanyId, usedAccountIds);

                var type = data.Votype?.ToUpper();
                SetupViewBagFlags(type);

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

        private void SetupViewBagFlags(string? votype)
        {
            var type = votype?.ToUpper();
            ViewBag.VoucherType = type;
            ViewBag.Type = type;
            ViewBag.IsReceipt = type == "CR" || type == "BR";
            ViewBag.IsFull = type == "JV" || type == "CP" || type == "BP";
            ViewBag.IsCashReceipt = type == "CR";
            ViewBag.IsBankReceipt = type == "BR";
            ViewBag.IsCashPayment = type == "CP";
            ViewBag.IsBankPayment = type == "BP";
            ViewBag.Votype = GetVoucherTitle(type);
        }

        // EDIT POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(VoucherVM vm)
        {
            if (vm?.Head == null)
            {
                TempData["ErrorMessage"] = "❌ Invalid voucher data submitted!";
                return RedirectToAction("Index", new { type = "" });
            }

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

                // Filter details that have an account selected and either debit or credit amount
                var submittedDetails = (vm.Details ?? new List<VoDet>())
                    .Where(x => x.gl3Id != null && x.gl3Id > 0 && ((x.Dramt ?? 0) != 0 || (x.Cramt ?? 0) != 0))
                    .ToList();

                if (setting != null && setting.AutoBalance)
                {
                    var dr = submittedDetails.Sum(x => x.Dramt ?? 0);
                    var cr = submittedDetails.Sum(x => x.Cramt ?? 0);

                    if (dr != cr && oldType == "JV")
                    {
                        TempData["ErrorMessage"] = "❌ Debit and Credit amounts must be equal!";
                        SetupViewBagFlags(oldType);
                        LoadDropdowns(oldType);
                        return View(vm);
                    }
                }

                var account = _context.GLChart3.FirstOrDefault(x => x.Id == vm.Head.gl3Id);
              
                if (account == null && oldType != "JV")
                {
                    TempData["ErrorMessage"] = "❌ Invalid account selected!";
                    SetupViewBagFlags(oldType);
                    LoadDropdowns(oldType);
                    return View(vm);
                }

                // =========================================
                // 🔥 STEP 1: UPDATE VOUCHER HEAD
                // =========================================
                existing.Narration = vm.Head.Narration;
                existing.Vodate = vm.Head.Vodate;
                existing.PersonName = vm.Head.PersonName;
                existing.Invno = vm.Head.Invno;
                existing.gl3Id = vm.Head.gl3Id;
                existing.Partycode = vm.Head.Partycode;
                existing.Totdramt = submittedDetails.Sum(x => x.Dramt ?? 0);
                existing.Totcramt = submittedDetails.Sum(x => x.Cramt ?? 0);

                decimal netDiff = (existing.Totdramt ?? 0) - (existing.Totcramt ?? 0);
                existing.Diff = netDiff;
                existing.Hdramt = netDiff < 0 ? Math.Abs(netDiff) : 0;
                existing.Hcramt = netDiff > 0 ? netDiff : 0;
                existing.Entries = submittedDetails.Count;

                int currentCompId = User.GetCompanyId();
                if (currentCompId > 0) existing.CompanyId = currentCompId;
                int currentFyId = User.GetFinancialYearId();
                if (currentFyId > 0) existing.FinancialYearId = currentFyId;
                string currentCoCode = User.GetCompanyCode();
                if (!string.IsNullOrEmpty(currentCoCode) && currentCoCode != "0") existing.Cocode = currentCoCode;

                existing.Userid = User.GetUserId();
                existing.Ac1 = account?.AC1;
                existing.Ac3 = account?.AC3;
                existing.Haccode = account?.ACC;
                existing.ModifiedOn = DateTime.Now;
                existing.ModifiedBy = GetUser();

                // =========================================
                // 🔥 STEP 2: SYNC DETAILS (NO EF TRACKING CONFLICT)
                // =========================================
                var submittedIds = submittedDetails.Where(x => x.Id > 0).Select(x => x.Id).ToHashSet();

                // Soft-delete rows that were deleted by user in the UI
                if (existing.Details != null)
                {
                    foreach (var d in existing.Details.Where(x => !x.IsDeleted))
                    {
                        if (!submittedIds.Contains(d.Id))
                        {
                            d.IsDeleted = true;
                            d.ModifiedOn = DateTime.Now;
                            d.ModifiedBy = GetUser();
                        }
                    }
                }

                var activeDetails = new List<VoDet>();

                foreach (var d in submittedDetails)
                {
                    var detailAccount = _context.GLChart3.FirstOrDefault(x => x.Id == d.gl3Id);
                    if (detailAccount == null)
                        continue;

                    VoDet target;

                    // If existing tracked detail entity, update in-place!
                    if (d.Id > 0 && existing.Details != null && existing.Details.Any(x => x.Id == d.Id))
                    {
                        target = existing.Details.First(x => x.Id == d.Id);
                        target.IsDeleted = false;
                        target.ModifiedOn = DateTime.Now;
                        target.ModifiedBy = GetUser();
                    }
                    else
                    {
                        // New detail row added in UI
                        target = new VoDet
                        {
                            VoHeadId = existing.Id,
                            CreatedOn = DateTime.Now,
                            CreatedBy = GetUser(),
                            IsDeleted = false
                        };
                        _context.VoDet.Add(target);
                    }

                    target.gl3Id = d.gl3Id;
                    target.Transcode = d.Transcode;
                    target.Billtino = d.Billtino;
                    target.Bilno = d.Bilno;
                    target.Vehicleno = d.Vehicleno;
                    target.Dramt = d.Dramt ?? 0;
                    target.Cramt = d.Cramt ?? 0;
                    target.Chqno = d.Chqno;
                    target.ChqDate = d.ChqDate;
                    target.Ptax = d.Ptax;
                    target.Narration = d.Narration;

                    target.Vono = existing.Vono;
                    target.Votype = existing.Votype;
                    target.Vodate = existing.Vodate;
                    target.Ac1 = detailAccount.AC1;
                    target.Ac3 = detailAccount.AC3;
                    target.Hacc = account?.ACC;
                    target.Acc = detailAccount.ACC;
                    target.Actype = detailAccount.AcType;
                    target.Cocode = existing.Cocode ?? User.GetCompanyCode();
                    target.Name = detailAccount.Name;
                    target.Ctype = detailAccount.CType;
                    target.EntryDate = vm.Head.EntryDate ?? existing.EntryDate;
                    target.Userid = User.GetUserId();

                    activeDetails.Add(target);
                }

                _context.SaveChanges();

                // =========================================
                // 🔥 STEP 3: REPOST GL TRANSACTIONS
                // =========================================
                _service.PostVoucher(existing, activeDetails);

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
                    ex.Message + (ex.InnerException != null ? " | Inner: " + ex.InnerException.Message : "")
                ).Wait();

                TempData["ErrorMessage"] = $"❌ Failed to update voucher: {ex.Message}";
                SetupViewBagFlags(oldType);
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

        private void LoadDropdowns(string votype, int? companyId = null, IEnumerable<int>? includeAccountIds = null)
        {
            try
            {
                int compId = companyId.HasValue && companyId.Value > 0 ? companyId.Value : User.GetCompanyId();
                int userCompId = User.GetCompanyId();
                var extraIds = includeAccountIds?.ToList() ?? new List<int>();

                ViewBag.Types = _context.VoucherTypeSettings.ToList();

                var partylist = _context.AcPara
                    .Where(a => AccountCategories.Party.Contains(a.ActypeCode)
                                && (a.CompanyId == compId || a.CompanyId == userCompId)
                                && a.Parent == "P")
                    .Select(a => a.Accode)
                    .Distinct();

                var partyAccounts = _context.GLChart3
                    .Where(g =>
                        g.AcType != "S" &&
                        (((g.CompanyId == compId || g.CompanyId == userCompId || g.CompanyId == 0 || g.CompanyId == null) && partylist.Contains(g.AC1)) ||
                         extraIds.Contains(g.Id))
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
                                && (a.CompanyId == compId || a.CompanyId == userCompId)
                                && a.Parent == "P")
                    .Select(a => a.Accode)
                    .Distinct();

                ViewBag.transporterList = _context.GLChart3
                    .Where(g =>
                        g.AcType != "S" &&
                        (((g.CompanyId == compId || g.CompanyId == userCompId || g.CompanyId == 0 || g.CompanyId == null) && transporterList.Contains(g.AC1)) ||
                         extraIds.Contains(g.Id))
                    )
                    .Select(g => new SelectListItem
                    {
                        Value = g.Id.ToString(),
                        Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
                    })
                    .OrderBy(x => x.Text)
                    .ToList();

                if (votype == "JV")
                {
                    ViewBag.HeaderAccounts = _context.GLChart3
                        .Where(g =>
                            g.AcType != "S" &&
                            ((g.CompanyId == compId || g.CompanyId == userCompId || g.CompanyId == 0 || g.CompanyId == null) ||
                             extraIds.Contains(g.Id))
                        )
                        .Select(g => new SelectListItem
                        {
                            Value = g.Id.ToString(),
                            Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
                        })
                        .OrderBy(x => x.Text)
                        .ToList();
                }
                else
                {
                    IEnumerable<string> allowedTypes = votype switch
                    {
                        "BP" or "BR" => AccountCategories.Bank,
                        "CP" or "CR" => AccountCategories.Cash,
                        _ => Array.Empty<string>()
                    };

                    var ac1List = _context.AcPara
                        .Where(a =>
                            allowedTypes.Contains(a.ActypeCode)
                            && (a.CompanyId == compId || a.CompanyId == userCompId)
                            && a.Parent == "P"
                        )
                        .Select(a => a.Accode)
                        .Distinct()
                        .ToList();

                    ViewBag.HeaderAccounts = _context.GLChart3
                        .Where(g =>
                            g.AcType != "S" &&
                            (((g.CompanyId == compId || g.CompanyId == userCompId || g.CompanyId == 0 || g.CompanyId == null) && ac1List.Contains(g.AC1)) ||
                             extraIds.Contains(g.Id))
                        )
                        .Select(g => new SelectListItem
                        {
                            Value = g.Id.ToString(),
                            Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
                        })
                        .OrderBy(x => x.Text)
                        .ToList();
                }

                ViewBag.Accounts = _context.GLChart3
                    .Where(g =>
                        g.AcType != "S" &&
                        ((g.CompanyId == compId || g.CompanyId == userCompId || g.CompanyId == 0 || g.CompanyId == null) ||
                         extraIds.Contains(g.Id))
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
