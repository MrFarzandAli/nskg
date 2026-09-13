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
    public class ToPayBiltyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AccountingService _service;
        private readonly IAuditService _audit;

        public ToPayBiltyController(ApplicationDbContext context, AccountingService service, IAuditService audit)
        {
            _context = context;
            _service = service;
            _audit = audit;
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

                // Filter only "ToPay" bilties
                var query = _context.IssHead
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.PType == "ToPay");

                int totalRecords = query.Count();

                if (!string.IsNullOrWhiteSpace(searchValue))
                {
                    searchValue = searchValue.Trim().ToLower();
                    query = query.Where(x =>
                        (x.DocNo != null && x.DocNo.ToLower().Contains(searchValue)) ||
                        (x.CusName != null && x.CusName.ToLower().Contains(searchValue))
                    );
                }

                int filterRecords = string.IsNullOrWhiteSpace(searchValue) ? totalRecords : query.Count();

                switch (sortColumnIndex)
                {
                    case "0":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.DocNo) : query.OrderByDescending(x => x.DocNo);
                        break;
                    case "1":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.DocDate).ThenBy(x => x.Id) : query.OrderByDescending(x => x.DocDate).ThenByDescending(x => x.Id);
                        break;
                    case "2":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.CusName) : query.OrderByDescending(x => x.CusName);
                        break;
                    default:
                        query = query.OrderByDescending(x => x.DocDate).ThenByDescending(x => x.Id);
                        break;
                }

                var rawData = query.Skip(skip).Take(pageSize)
                    .Select(b => new { b.Id, b.DocNo, b.DocDate, b.CusName })
                    .ToList();

                var data = rawData.Select(b => new
                {
                    id = b.Id,
                    docNo = b.DocNo ?? "",
                    docDate = b.DocDate.HasValue ? b.DocDate.Value.ToString("dd-MMM-yyyy") : "",
                    cusName = b.CusName ?? ""
                }).ToList();

                return Json(new { draw = drawVal, recordsTotal = totalRecords, recordsFiltered = filterRecords, data = data });
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "ToPayBilty", "0", ex.Message).Wait();
                int drawVal = 0;
                int.TryParse(Request.Form["draw"].FirstOrDefault(), out drawVal);
                return Json(new { draw = drawVal, recordsTotal = 0, recordsFiltered = 0, data = new List<object>() });
            }
        }

        public IActionResult Create()
        {
            LoadDropdowns();
            return View(new BiltyViewModel
            {
                Head = new IssHead
                {
                    DocDate = DateTime.Now,
                    DocNo = GenerateDocNo()
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
                model.Details = model.Details?
                    .Where(x => !string.IsNullOrWhiteSpace(x.IName) && x.Qty > 0)
                    .ToList() ?? new List<BiltyDetailVM>();

                ModelState.Clear();

                if (!model.Details.Any())
                    ModelState.AddModelError("", "At least one bilty item is required.");

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
                model.Head.PType = "ToPay"; // ← Key difference

                _context.IssHead.Add(model.Head);
                _context.SaveChanges();

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

                _audit.LogAsync("Create", "ToPayBilty", model.Head.Id.ToString(), $"To Pay Bilty Created: {model.Head.DocNo}").Wait();

                TempData["SuccessMessage"] = "✅ To Pay Bilty saved successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "ToPayBilty Create", "0", ex.Message).Wait();
                TempData["ErrorMessage"] = "❌ Failed to save To Pay Bilty.";
                LoadDropdowns();
                return View(model);
            }
        }

        public IActionResult Edit(int id)
        {
            LoadDropdowns();

            var head = _context.IssHead.FirstOrDefault(x => x.Id == id);
            if (head == null) return NotFound();

            var details = _context.IssDetail
                .Where(x => x.IssHeadId == id && !x.IsDeleted)
                .Select(x => new BiltyDetailVM
                {
                    IName = x.IName ?? "",
                    Qty = x.Qty ?? 0,
                    QtyPerPack = x.QtyPerPack ?? 0
                })
                .ToList();

            return View(new BiltyViewModel
            {
                Head = head,
                Details = details ?? new List<BiltyDetailVM>()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(BiltyViewModel model)
        {
            try
            {
                model.Details = model.Details?
                    .Where(x => !string.IsNullOrWhiteSpace(x.IName) && x.Qty > 0)
                    .ToList() ?? new List<BiltyDetailVM>();

                ModelState.Clear();

                if (!model.Details.Any())
                    ModelState.AddModelError("", "At least one bilty item is required.");

                if (!TryValidateModel(model))
                {
                    LoadDropdowns();
                    return View(model);
                }

                using var transaction = _context.Database.BeginTransaction();

                var head = _context.IssHead
                    .Include(x => x.Details.Where(d => !d.IsDeleted))
                    .FirstOrDefault(x => x.Id == model.Head.Id);

                if (head == null) return NotFound();

                head.BillTiNo = model.Head.BillTiNo;
                head.BilNo = model.Head.BilNo;
                head.DocDate = model.Head.DocDate;
                head.StationId = model.Head.StationId;
                head.CustomerId = model.Head.CustomerId;
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
                head.NetAmt = (model.Head.Cartage1 ?? 0) + (model.Head.Cartage2 ?? 0) + (model.Head.Cartage3 ?? 0) + (model.Head.Labour ?? 0) + (model.Head.T_T ?? 0);
                head.ModifiedOn = DateTime.Now;
                head.ModifiedBy = GetUser();

                foreach (var d in head.Details)
                {
                    d.IsDeleted = true;
                    d.ModifiedOn = DateTime.Now;
                    d.ModifiedBy = GetUser();
                    _context.IssDetail.Update(d);
                }
                _context.SaveChanges();

                foreach (var d in model.Details ?? new List<BiltyDetailVM>())
                {
                    _context.IssDetail.Add(new IssDetail
                    {
                        IssHeadId = head.Id,
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            try
            {
                var v = _context.IssHead.Include(x => x.Details).FirstOrDefault(x => x.Id == id);
                if (v == null) return Json(new { success = false, message = "To Pay Bilty not found!" });

                var gl = _context.GLTrans.Where(x => x.RefId == v.Id && (x.RefType == "BL" || x.RefType == "SL" || x.RefType == "WT"));
                _context.GLTrans.RemoveRange(gl);

                foreach (var d in v.Details)
                {
                    d.IsDeleted = true;
                    d.ModifiedOn = DateTime.Now;
                    d.ModifiedBy = GetUser();
                    _context.IssDetail.Update(d);
                }

                v.IsDeleted = true;
                v.ModifiedOn = DateTime.Now;
                v.ModifiedBy = GetUser();
                _context.IssHead.Update(v);
                _context.SaveChanges();

                _audit.LogAsync("Delete", "ToPayBilty", id.ToString(), $"Soft Deleted: {v.DocNo}").Wait();

                return Json(new { success = true, message = "To Pay Bilty deleted successfully!" });
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "ToPayBilty", id.ToString(), ex.Message).Wait();
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
                    .Select(a => a.Accode).Distinct();

                var customerAccounts = _context.GLChart3
                    .Where(g => g.CompanyId == User.GetCompanyId() && g.AcType != "S" && customerlist.Contains(g.AC1))
                    .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name + " (" + (g.AC1 + g.AC3) + ")" })
                    .OrderBy(x => x.Text).ToList();

                customerAccounts.Insert(0, new SelectListItem { Value = "", Text = "-- Select Customer Account --" });
                ViewBag.Customers = customerAccounts;

                var stationList = _context.AcPara
                    .Where(a => AccountCategories.Station.Contains(a.ActypeCode)
                                && a.CompanyId == User.GetCompanyId()
                                && a.Parent == "P")
                    .Select(a => a.Accode).Distinct();

                var stationAccounts = _context.GLChart3
                    .Where(g => g.CompanyId == User.GetCompanyId() && g.AcType != "S" && stationList.Contains(g.AC1))
                    .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name + " (" + (g.AC1 + g.AC3) + ")" })
                    .OrderBy(x => x.Text).ToList();

                stationAccounts.Insert(0, new SelectListItem { Value = "", Text = "-- Select Station Account --" });
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
            var fy = _context.FinancialYears.FirstOrDefault(x => x.Id == User.GetFinancialYearId());
            if (fy == null) throw new Exception("Active financial year not found.");

            string monthPart = DateTime.Now.ToString("MM");
            string yearPart = fy.StartDate.ToString("yy");
            string code = monthPart + yearPart;

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
