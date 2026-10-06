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

namespace Nskg.Controllers
{
    public class BiltyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AccountingService _service;
        private readonly IAuditService _audit;

        public BiltyController(ApplicationDbContext context, AccountingService service, IAuditService audit)
        {
            _context = context;
            _service = service;
            _audit = audit; // ✅ ADD
        }
        private string GetUser()
        {
            return User?.Identity?.Name ?? "System";
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult GetBiltyList()
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
                int drawVal = 0;
                int.TryParse(draw, out drawVal);

                int companyId = User.GetCompanyId();
                string companyCode = User.GetCompanyCode();

                var query = _context.IssHead
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && (x.PType == "Paid" || x.PType == null));

                if (companyId > 0)
                {
                    query = query.Where(x => x.CompanyId == companyId || (!string.IsNullOrEmpty(companyCode) && companyCode != "0" && x.CoCode == companyCode));
                }

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrWhiteSpace(searchValue))
                {
                    searchValue = searchValue.Trim().ToLower();
                    query = query.Where(x =>
                        (x.DocNo != null && x.DocNo.ToLower().Contains(searchValue)) ||
                        (x.CusName != null && x.CusName.ToLower().Contains(searchValue)) ||
                        (x.BillTiNo != null && x.BillTiNo.ToString().Contains(searchValue)) ||
                        (x.BilNo != null && x.BilNo.ToString().Contains(searchValue))
                    );
                }

                // Column-wise Searching
                var col0Search = Request.Form["columns[0][search][value]"].FirstOrDefault();
                var col1Search = Request.Form["columns[1][search][value]"].FirstOrDefault();
                var col2Search = Request.Form["columns[2][search][value]"].FirstOrDefault();
                var col3Search = Request.Form["columns[3][search][value]"].FirstOrDefault();
                var col4Search = Request.Form["columns[4][search][value]"].FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(col0Search))
                {
                    var val0 = col0Search.Trim().ToLower();
                    query = query.Where(x => x.DocNo != null && x.DocNo.ToLower().Contains(val0));
                }

                if (!string.IsNullOrWhiteSpace(col1Search))
                {
                    var val1 = col1Search.Trim();
                    query = query.Where(x => x.BillTiNo != null && x.BillTiNo.ToString().Contains(val1));
                }

                if (!string.IsNullOrWhiteSpace(col2Search))
                {
                    var val2 = col2Search.Trim();
                    query = query.Where(x => x.BilNo != null && x.BilNo.ToString().Contains(val2));
                }

                if (!string.IsNullOrWhiteSpace(col3Search))
                {
                    var val3 = col3Search.Trim();
                    if (DateTime.TryParse(val3, out var parsedDate))
                    {
                        var targetDate = parsedDate.Date;
                        query = query.Where(x => x.DocDate.HasValue && x.DocDate.Value.Date == targetDate);
                    }
                    else
                    {
                        query = query.Where(x => x.DocDate.HasValue && x.DocDate.Value.ToString().Contains(val3));
                    }
                }

                if (!string.IsNullOrWhiteSpace(col4Search))
                {
                    var val4 = col4Search.Trim().ToLower();
                    query = query.Where(x => x.CusName != null && x.CusName.ToLower().Contains(val4));
                }

                int filterRecords = query.Count();

                // Sorting
                switch (sortColumnIndex)
                {
                    case "0":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.DocNo) : query.OrderByDescending(x => x.DocNo);
                        break;
                    case "1":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.BillTiNo).ThenByDescending(x => x.DocDate).ThenBy(x => x.Id) : query.OrderByDescending(x => x.BillTiNo).ThenByDescending(x => x.DocDate).ThenByDescending(x => x.Id);
                        break;
                    case "2":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.BilNo).ThenByDescending(x => x.DocDate).ThenBy(x => x.Id) : query.OrderByDescending(x => x.BilNo).ThenByDescending(x => x.DocDate).ThenByDescending(x => x.Id);
                        break;
                    case "3":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.DocDate).ThenBy(x => x.Id) : query.OrderByDescending(x => x.DocDate).ThenByDescending(x => x.Id);
                        break;
                    case "4":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.CusName) : query.OrderByDescending(x => x.CusName);
                        break;
                    default:
                        query = query.OrderByDescending(x => x.DocDate).ThenByDescending(x => x.Id);
                        break;
                }

                var rawData = query.Skip(skip).Take(pageSize)
                    .Select(b => new
                    {
                        b.Id,
                        b.DocNo,
                        b.BillTiNo,
                        b.BilNo,
                        b.DocDate,
                        b.CusName
                    })
                    .ToList();

                var data = rawData.Select(b => new
                {
                    id = b.Id,
                    docNo = b.DocNo ?? "",
                    biltyNo = b.BillTiNo.HasValue ? (b.BillTiNo.Value % 1 == 0 ? b.BillTiNo.Value.ToString("0") : b.BillTiNo.Value.ToString()) : "",
                    bilNo = b.BilNo.HasValue ? (b.BilNo.Value % 1 == 0 ? b.BilNo.Value.ToString("0") : b.BilNo.Value.ToString()) : "",
                    docDate = b.DocDate.HasValue ? b.DocDate.Value.ToString("dd-MMM-yyyy") : "",
                    cusName = b.CusName ?? ""
                }).ToList();

                return Json(new
                {
                    draw = drawVal,
                    recordsTotal = totalRecords,
                    recordsFiltered = filterRecords,
                    data = data
                });
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "Bilty", "0", ex.Message).Wait();
                int drawVal = 0;
                int.TryParse(Request.Form["draw"].FirstOrDefault(), out drawVal);
                return Json(new { draw = drawVal, recordsTotal = 0, recordsFiltered = 0, data = new List<object>() });
            }
        }

        public IActionResult Create()
        {
            int companyId = User.GetCompanyId();
            string companyCode = User.GetCompanyCode();

            LoadDropdowns();

            var last = _context.IssHead
                .Where(x => !x.IsDeleted && (x.PType == "Paid" || x.PType == null) && (companyId <= 0 || x.CompanyId == companyId || (!string.IsNullOrEmpty(companyCode) && companyCode != "0" && x.CoCode == companyCode)))
                .OrderByDescending(x => x.DocDate)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();

            if (last == null)
            {
                last = _context.IssHead.Where(x => !x.IsDeleted && (x.PType == "Paid" || x.PType == null))
                    .OrderByDescending(x => x.DocDate)
                    .ThenByDescending(x => x.Id)
                    .FirstOrDefault();
            }

            return View(new BiltyViewModel
            {
                Head = new IssHead
                {
                    DocDate = last?.DocDate ?? DateTime.Now,
                    DocNo = GenerateDocNo(),
                    StationId = last?.StationId ?? 0,
                    CustomerId = last?.CustomerId ?? 0,
                    SendTo = last?.SendTo,
                    PType = "Paid"
                },
                Details = new List<BiltyDetailVM>()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(BiltyViewModel model)
        {
            try
            {
                // Remove empty detail rows
                model.Details = model.Details?
                    .Where(x => !string.IsNullOrWhiteSpace(x.IName)
                             && x.Qty > 0)
                    .ToList() ?? new List<BiltyDetailVM>();

                // 🔥 CLEAR ModelState errors before validation
                ModelState.Clear();

                if (!model.Details.Any())
                {
                    ModelState.AddModelError("", "At least one bilty item is required.");
                }

                // Re-validate the model after clearing and filtering
                if (!TryValidateModel(model))
                {
                    LoadDropdowns();
                    return View(model);
                }

                using var transaction = _context.Database.BeginTransaction();

                var DetailAccount = model.Head.CustomerId > 0 ? _context.GLChart3.FirstOrDefault(x => x.Id == model.Head.CustomerId) : null;
                var FooderlAccount = model.Head.StationId > 0 ? _context.GLChart3.FirstOrDefault(x => x.Id == model.Head.StationId) : null;
                var SalesAccount = _context.AcPara.Where(a => AccountCategories.Sales.Contains(a.ActypeCode)
                   && a.CompanyId == User.GetCompanyId()
                   && a.Parent == "P").Select(a => a.Accode).Distinct().FirstOrDefault();

                // Security / consistency fields
                model.Head.DocNo = GenerateDocNo();
                model.Head.CompanyId = User.GetCompanyId();
                model.Head.CoCode = User.GetCompanyCode();
                model.Head.UserId = User.GetUserId();
                model.Head.FyId = User.GetFinancialYearId();
                model.Head.CusName = DetailAccount?.Name;
                model.Head.CusCode = DetailAccount?.ACC;
                model.Head.Fooder = FooderlAccount?.Name;
                model.Head.FooderCode = FooderlAccount?.ACC;
                model.Head.Qty = model.Details.Sum(x => x.Qty);
                model.Head.AccCode = SalesAccount;
                model.Head.CreatedOn = DateTime.Now;
                model.Head.CreatedBy = GetUser();
                model.Head.IsDeleted = false;
                model.Head.PType = "Paid";

                // Save Head
                _context.IssHead.Add(model.Head);
                _context.SaveChanges();

                // Save Details                
                foreach (var detail in model.Details)
                {
                    var issDetail = new IssDetail
                    {
                        DocNo = model.Head.DocNo,
                        DocDate = model.Head.DocDate,
                        IssHeadId = model.Head.Id,
                        CompanyId = model.Head.CompanyId,
                        UserId = model.Head.UserId,
                        FyId = model.Head.FyId,
                        CusName = DetailAccount?.Name,
                        CusCode = DetailAccount?.ACC,
                        IName = detail.IName,
                        Qty = detail.Qty,
                        QtyPerPack = detail.QtyPerPack,
                        AccCode = SalesAccount,
                        CreatedOn = DateTime.Now,
                        CreatedBy = GetUser(),
                        IsDeleted = false
                    };

                    _context.IssDetail.Add(issDetail);
                }

                var insertedDetails = _context.IssDetail.Where(x => x.IssHeadId == model.Head.Id && !x.IsDeleted).ToList();
                _service.PostBilty(model.Head, insertedDetails);

                transaction.Commit();

                _audit.LogAsync(
                    "Create",
                    "Bilty",
                    model.Head.Id.ToString(),
                    $"Bilty Created: {model.Head.DocNo}"
                ).Wait();

                TempData["SuccessMessage"] = "✅ Bilty saved successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "Bilty Create", "0", ex.Message).Wait();

                TempData["ErrorMessage"] = "❌ Failed to save bilty.";

                LoadDropdowns();
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult GetBiltyId(string? docNo, string? billTiNo)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(docNo))
                {
                    var b = _context.IssHead.AsNoTracking().FirstOrDefault(x => x.DocNo == docNo.Trim() && !x.IsDeleted);
                    if (b != null) return Json(new { success = true, id = b.Id });
                }

                if (!string.IsNullOrWhiteSpace(billTiNo))
                {
                    if (decimal.TryParse(billTiNo.Trim(), out var bVal))
                    {
                        var b = _context.IssHead.AsNoTracking().FirstOrDefault(x => x.BillTiNo == bVal && !x.IsDeleted);
                        if (b != null) return Json(new { success = true, id = b.Id });
                    }
                }

                return Json(new { success = false, message = "Bilty not found" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public IActionResult Edit(int id)
        {
            LoadDropdowns();

            var head = _context.IssHead
                .FirstOrDefault(x => x.Id == id);

            if (head == null)
                return NotFound();

            var details = _context.IssDetail
                .Where(x => x.IssHeadId == id
                   && !x.IsDeleted)  // Filter active details
                .Select(x => new BiltyDetailVM
                {
                    IName = x.IName ?? "",
                    Qty = x.Qty ?? 0,
                    QtyPerPack = x.QtyPerPack ?? 0
                })
                .ToList();

            int compId = head.CompanyId > 0 ? head.CompanyId : User.GetCompanyId();
            DateTime docDate = head.DocDate ?? DateTime.Today;

            // Next entry (next row down in list: older date, or same date with lower Id)
            var nextId = _context.IssHead
                .Where(x => !x.IsDeleted && (x.PType == "Paid" || x.PType == null) && (compId <= 0 || x.CompanyId == compId || (head.CoCode != null && x.CoCode == head.CoCode)) &&
                    (x.DocDate < docDate || (x.DocDate == docDate && x.Id < id)))
                .OrderByDescending(x => x.DocDate)
                .ThenByDescending(x => x.Id)
                .Select(x => x.Id)
                .FirstOrDefault();

            // Prev entry (previous row up in list: newer date, or same date with higher Id)
            var prevId = _context.IssHead
                .Where(x => !x.IsDeleted && (x.PType == "Paid" || x.PType == null) && (compId <= 0 || x.CompanyId == compId || (head.CoCode != null && x.CoCode == head.CoCode)) &&
                    (x.DocDate > docDate || (x.DocDate == docDate && x.Id > id)))
                .OrderBy(x => x.DocDate)
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
                .FirstOrDefault();

            ViewBag.PrevId = prevId > 0 ? prevId : (int?)null;
            ViewBag.NextId = nextId > 0 ? nextId : (int?)null;

            var model = new BiltyViewModel
            {
                Head = head,
                Details = details ?? new List<BiltyDetailVM>()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(BiltyViewModel model)
        {
            try
            {

                // Remove empty detail rows
                model.Details = model.Details?
                    .Where(x => !string.IsNullOrWhiteSpace(x.IName)
                             && x.Qty > 0)
                    .ToList() ?? new List<BiltyDetailVM>();

                // 🔥 CLEAR ModelState errors before validation
                ModelState.Clear();

                if (!model.Details.Any())
                {
                    ModelState.AddModelError("", "At least one bilty item is required.");
                }

                // Re-validate the model after clearing and filtering
                if (!TryValidateModel(model))
                {
                    LoadDropdowns();
                    return View(model);
                }

                using var transaction = _context.Database.BeginTransaction();

                var head = _context.IssHead
                       .Include(x => x.Details.Where(d => !d.IsDeleted))  // Filter active details
                    .FirstOrDefault(x => x.Id == model.Head.Id);

                if (head == null)
                    return NotFound();

                // MASTER
                var DetailAccount = model.Head.CustomerId > 0 ? _context.GLChart3.FirstOrDefault(x => x.Id == model.Head.CustomerId) : null;
                var FooderAccount = model.Head.StationId > 0 ? _context.GLChart3.FirstOrDefault(x => x.Id == model.Head.StationId) : null;

                head.BillTiNo = model.Head.BillTiNo;
                head.BilNo = model.Head.BilNo;
                head.DocDate = model.Head.DocDate;
                head.StationId = model.Head.StationId;
                head.CustomerId = model.Head.CustomerId;
                head.CusName = DetailAccount?.Name;
                head.CusCode = DetailAccount?.ACC;
                head.Fooder = FooderAccount?.Name;
                head.FooderCode = FooderAccount?.ACC;
                head.Qty = model.Details?.Sum(x => x.Qty) ?? 0;
                head.Narration = model.Head.Narration;
                head.SendTo = model.Head.SendTo;

                head.Cartage1 = model.Head.Cartage1;
                head.Cartage2 = model.Head.Cartage2;
                head.Cartage3 = model.Head.Cartage3;
                head.Labour = model.Head.Labour;
                head.T_T = model.Head.T_T;

                head.PartyEx = model.Head.PartyEx;
                head.Lifter2 = model.Head.Lifter2;
                head.OtherEx = model.Head.OtherEx;

                // IMPORTANT FIX
                head.NetAmt = (model.Head.Cartage1 ?? 0)
                            + (model.Head.Cartage2 ?? 0)
                            + (model.Head.Cartage3 ?? 0)
                            + (model.Head.Labour ?? 0)
                            + (model.Head.T_T ?? 0);
                head.ModifiedOn = DateTime.Now;
                head.ModifiedBy = GetUser();
                // DELETE OLD DETAILS
                // _context.IssDetail.RemoveRange(head.Details);
                foreach (var d in head.Details)
                {
                    d.IsDeleted = true;
                    d.ModifiedOn = DateTime.Now;
                    d.ModifiedBy = GetUser();

                    _context.IssDetail.Update(d);
                }
                _context.SaveChanges();

                // INSERT NEW DETAILS
                foreach (var d in model.Details ?? new List<BiltyDetailVM>())
                {
                    _context.IssDetail.Add(new IssDetail
                    {
                        DocNo = head.DocNo,
                        DocDate = head.DocDate,
                        IssHeadId = head.Id,
                        CompanyId = head.CompanyId,
                        UserId = head.UserId,
                        FyId = head.FyId,
                        CusName = DetailAccount?.Name,
                        CusCode = DetailAccount?.ACC,
                        AccCode = head.AccCode,
                        IName = d.IName,
                        Qty = d.Qty,
                        QtyPerPack = d.QtyPerPack,
                        CreatedOn = DateTime.Now,
                        CreatedBy = GetUser(),
                        IsDeleted = false
                    });
                }

                _context.SaveChanges();

                var activeDetails = _context.IssDetail.Where(x => x.IssHeadId == head.Id && !x.IsDeleted).ToList();
                _service.PostBilty(head, activeDetails);

                transaction.Commit();

                TempData["SuccessMessage"] = "Updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                LoadDropdowns();
                return View(model);
            }
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult Delete(int id)
        //{
        //    try
        //    {
        //        var v = _context.IssHead
        //            .Include(x => x.Details)
        //            .FirstOrDefault(x => x.Id == id);

        //        if (v == null)
        //        {
        //            return Json(new { success = false, message = "Bilty not found!" });
        //        }

        //        var gl = _context.GLTrans
        //            .Where(x => x.RefId == v.Id);

        //        _context.GLTrans.RemoveRange(gl);

        //        _context.IssDetail.RemoveRange(v.Details);
        //        _context.IssHead.Remove(v);

        //        _context.SaveChanges();

        //        _audit.LogAsync("Delete", "Bilty", id.ToString(),
        //            $"Deleted: {v.DocNo}").Wait();

        //        return Json(new { success = true, message = "Bilty deleted successfully!" });
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
                var v = _context.IssHead
                    .Include(x => x.Details)
                    .FirstOrDefault(x => x.Id == id);

                if (v == null)
                {
                    return Json(new { success = false, message = "Bilty not found!" });
                }

                // 🔥 GL REMOVE
                var gl = _context.GLTrans
                    .Where(x => x.RefId == v.Id && (x.RefType == "BL" || x.RefType == "SL" || x.RefType == "WT"));

                _context.GLTrans.RemoveRange(gl);

                // 🔥 SOFT DELETE DETAILS
                foreach (var d in v.Details)
                {
                    d.IsDeleted = true;
                    d.ModifiedOn = DateTime.Now;
                    d.ModifiedBy = GetUser();

                    _context.IssDetail.Update(d);
                }

                // 🔥 SOFT DELETE HEAD
                v.IsDeleted = true;
                v.ModifiedOn = DateTime.Now;
                v.ModifiedBy = GetUser();

                _context.IssHead.Update(v);

                _context.SaveChanges();

                _audit.LogAsync("Delete", "Bilty", id.ToString(),
                    $"Soft Deleted: {v.DocNo}").Wait();

                return Json(new { success = true, message = "Bilty deleted successfully!" });
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "Bilty", id.ToString(), ex.Message).Wait();

                return Json(new { success = false, message = ex.Message });
            }
        }
        private void LoadDropdowns()
        {
            try
            {
               

                var customerlist = _context.AcPara
    .Where(a => AccountCategories.Customer.Contains(a.ActypeCode)
                && a.CompanyId == User.GetCompanyId()
                && a.Parent == "P")
    .Select(a => a.Accode)
    .Distinct();

                var customerAccounts = _context.GLChart3
                    .Where(g =>
                        g.CompanyId == User.GetCompanyId() &&
                        g.AcType != "S" &&
                        customerlist.Contains(g.AC1)
                    )
                    .Select(g => new SelectListItem
                    {
                        Value = g.Id.ToString(),
                        Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
                    })
                    .OrderBy(x => x.Text)
                    .ToList();

                // ✅ Add default item at index 0
                customerAccounts.Insert(0, new SelectListItem
                {
                    Value = "",
                    Text = "-- Select Customer Account --"
                });

                ViewBag.Customers = customerAccounts;

                var stationList = _context.AcPara
                    .Where(a => AccountCategories.Station.Contains(a.ActypeCode)
                                && a.CompanyId == User.GetCompanyId()
                                && a.Parent == "P")
                    .Select(a => a.Accode)
                    .Distinct();

                var stationAccounts = _context.GLChart3
                    .Where(g =>
                        g.CompanyId == User.GetCompanyId() &&
                        g.AcType != "S" &&
                        stationList.Contains(g.AC1)
                    )
                    .Select(g => new SelectListItem
                    {
                        Value = g.Id.ToString(),
                        Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
                    })
                    .OrderBy(x => x.Text)
                    .ToList();

                stationAccounts.Insert(0, new SelectListItem
                {
                    Value = "",
                    Text = "-- Select Station Account --"
                });

                ViewBag.Stations = stationAccounts;
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "LoadDropdowns", "0", ex.Message).Wait();
                throw;
            }
        }
        private string GenerateDocNo()
        {
            var fy = _context.FinancialYears
                .FirstOrDefault(x =>
                    x.Id == User.GetFinancialYearId());

            if (fy == null)
                throw new Exception("Active financial year not found.");

            string monthPart = DateTime.Now.ToString("MM");
            string yearPart = fy.StartDate.ToString("yy");

            string code = monthPart + yearPart;   // e.g. 0426

            var lastDoc = _context.IssHead
                .Where(x => x.DocNo.EndsWith("/" + code))
                .OrderByDescending(x => x.Id)
                .Select(x => x.DocNo)
                .FirstOrDefault();

            int nextNumber = 1;

            if (!string.IsNullOrEmpty(lastDoc))
            {
                var numericPart = lastDoc.Split('/')[0];

                if (int.TryParse(numericPart, out int lastNumber))
                    nextNumber = lastNumber + 1;
            }

            return $"{nextNumber:D4}/{code}";
        }
    }
}