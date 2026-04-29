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
    public class ChallanController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AccountingService _service;
        private readonly IAuditService _audit;

        public ChallanController(ApplicationDbContext context, AccountingService service, IAuditService audit)
        {
            _context = context;
            _service = service;
            _audit = audit; // ✅ ADD
        }
        public IActionResult Index()
        {
            try
            {
                var data = _context.ChallanHead
                    .ToList();

                return View(data);
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "Challan", "0", ex.Message).Wait();
                TempData["ErrorMessage"] = "❌ Failed to load challans!";
                return View(new List<ChallanHead>());
            }
        }

        public IActionResult Create()
        {
            LoadDropdowns();

            return View(new ChallanViewModel
            {
                Head = new ChallanHead
                {
                    DocDate = DateTime.Now,
                    DocNo = GenerateDocNo()

                },
                Details = new List<ChallanDetailVM>()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(ChallanViewModel model)
        {
            try
            {
                // Remove empty detail rows
                model.Details = model.Details?
                    .Where(x => !string.IsNullOrWhiteSpace(x.IName)
                             && x.Qty > 0)
                    .ToList() ?? new List<ChallanDetailVM>();

               
                if (!model.Details.Any())
                {
                    ModelState.AddModelError("", "At least one challan item is required.");
                }

                if (!ModelState.IsValid)
                {
                    LoadDropdowns();
                    return View(model);
                }

                using var transaction = _context.Database.BeginTransaction();

                //var DetailAccount = _context.GLChart3.FirstOrDefault(x => x.Id == model.Head.CustomerId);
                //var FooderlAccount = _context.GLChart3.FirstOrDefault(x => x.Id == model.Head.CustomerId);
                //var SalesAccount = _context.AcPara.Where(a => AccountCategories.Sales.Contains(a.ActypeCode)
                //   && a.Cocode == User.GetCompanyId().ToString()
                //   && a.Parent == "P").Select(a => a.Accode).Distinct().FirstOrDefault();

                //// Security / consistency fields
                //model.Head.DocNo = GenerateDocNo();
                //model.Head.CompanyId = User.GetCompanyId();
                //model.Head.CoCode = User.GetCompanyCode();
                //model.Head.UserId = User.GetUserId();
                //model.Head.FyId = User.GetFinancialYearId();
                //model.Head.CusName = DetailAccount.Name;
                //model.Head.CusCode = DetailAccount.ACC;
                //model.Head.Fooder = FooderlAccount.Name;
                //model.Head.FooderCode = FooderlAccount.ACC;
                //model.Head.Qty = model.Details.Sum(x => x.Qty);
                //model.Head.AccCode = SalesAccount;

                //// Save Head
                //_context.ChallanHead.Add(model.Head);
                //_context.SaveChanges();

                
                //// Save Details                
                //foreach (var detail in model.Details)
                //{
                //    var challanDetail = new ChallanDet
                //    {
                //        DocNo = model.Head.DocNo,
                //        DocDate = model.Head.DocDate,
                //        ChallanHeadId = model.Head.Id,
                //        CompanyId = model.Head.CompanyId,
                //        UserId = model.Head.UserId,
                //        FyId = model.Head.FyId,
                //        CusName = DetailAccount.Name,
                //        CusCode = DetailAccount.ACC,
                //        IName = detail.IName,
                //        Qty = detail.Qty,
                //        QtyPerPack = detail.QtyPerPack,
                //        AccCode = SalesAccount
                //    };

                //    _context.ChallanDet.Add(challanDetail);
                //}

                //_context.SaveChanges();

                //transaction.Commit();

                _audit.LogAsync(
                    "Create",
                    "Challan",
                    model.Head.Id.ToString(),
                    $"Challan Created: {model.Head.DocNo}"
                ).Wait();

                TempData["SuccessMessage"] = "✅ Challan saved successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "Challan Create", "0", ex.Message).Wait();

                TempData["ErrorMessage"] = "❌ Failed to save challan.";

                LoadDropdowns();
                return View(model);
            }
        }

        //public IActionResult Edit(int id)
        //{
        //    LoadDropdowns();

        //    var head = _context.ChallanHead
        //        .FirstOrDefault(x => x.Id == id);

        //    if (head == null)
        //        return NotFound();

        //    var details = _context.ChallanDet
        //        .Where(x => x.ChallanHeadId == id)
        //        .Select(x => new ChallanDetailVM
        //        {
        //            IName = x.IName ?? "",
        //            Qty = x.Qty ?? 0,
        //            QtyPerPack = x.QtyPerPack ?? 0
        //        })
        //        .ToList();

        //    var model = new ChallanViewModel
        //    {
        //        Head = head,
        //        Details = details ?? new List<ChallanDetailVM>()
        //    };

        //    return View(model);
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult Edit(ChallanViewModel model)
        //{
        //    try
        //    {
        //        if (!ModelState.IsValid)
        //        {
        //            LoadDropdowns();
        //            return View(model);
        //        }

        //        using var transaction = _context.Database.BeginTransaction();

        //        var head = _context.ChallanHead
        //            .Include(x => x.Details)
        //            .FirstOrDefault(x => x.Id == model.Head.Id);

        //        if (head == null)
        //            return NotFound();

        //        // MASTER
        //        head.BillTiNo = model.Head.BillTiNo;
        //        head.BilNo = model.Head.BilNo;
        //        head.DocDate = model.Head.DocDate;
        //        head.StationId = model.Head.StationId;
        //        head.CustomerId = model.Head.CustomerId;
        //        head.Narration = model.Head.Narration;
        //        head.SendTo = model.Head.SendTo;

        //        head.Cartage1 = model.Head.Cartage1;
        //        head.Cartage2 = model.Head.Cartage2;
        //        head.Cartage3 = model.Head.Cartage3;
        //        head.Labour = model.Head.Labour;
        //        head.T_T = model.Head.T_T;

        //        head.PartyEx = model.Head.PartyEx;
        //        head.Lifter2 = model.Head.Lifter2;
        //        head.OtherEx = model.Head.OtherEx;

        //        // IMPORTANT FIX
        //        head.NetAmt = (model.Head.Cartage1 ?? 0)
        //                    + (model.Head.Cartage2 ?? 0)
        //                    + (model.Head.Cartage3 ?? 0)
        //                    + (model.Head.Labour ?? 0)
        //                    + (model.Head.T_T ?? 0);

        //        // DELETE OLD DETAILS
        //        _context.ChallanDet.RemoveRange(head.Details);
        //        _context.SaveChanges();

        //        // INSERT NEW DETAILS
        //        foreach (var d in model.Details ?? new List<ChallanDetailVM>())
        //        {
        //            _context.ChallanDet.Add(new ChallanDet
        //            {
        //                ChallanHeadId = head.Id,
        //                IName = d.IName,
        //                Qty = d.Qty,
        //                QtyPerPack = d.QtyPerPack
        //            });
        //        }

        //        _context.SaveChanges();
        //        transaction.Commit();

        //        TempData["SuccessMessage"] = "Updated successfully!";
        //        return RedirectToAction("Index");
        //    }
        //    catch (Exception ex)
        //    {
        //        TempData["ErrorMessage"] = ex.Message;
        //        LoadDropdowns();
        //        return View(model);
        //    }
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            try
            {
                var v = _context.ChallanHead
                    .Include(x => x.Details)
                    .FirstOrDefault(x => x.Id == id);

                if (v == null)
                {
                    return Json(new { success = false, message = "Challan not found!" });
                }

                var gl = _context.GLTrans
                    .Where(x => x.RefId == v.Id);

                _context.GLTrans.RemoveRange(gl);

                _context.ChallanDet.RemoveRange(v.Details);
                _context.ChallanHead.Remove(v);

                _context.SaveChanges();

                _audit.LogAsync("Delete", "challan", id.ToString(),
                    $"Deleted: {v.DocNo}").Wait();

                return Json(new { success = true, message = "Challan deleted successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private void LoadDropdowns()
        {
            try
            {
               

                var transporterlist = _context.AcPara
    .Where(a => AccountCategories.Transporter.Contains(a.ActypeCode)
                && a.Cocode == User.GetCompanyId().ToString()
                && a.Parent == "P")
    .Select(a => a.Accode)
    .Distinct();

                var transporterAccounts = _context.GLChart3
                    .Where(g =>
                        g.CoCode == User.GetCompanyId().ToString() &&
                        g.AcType != "S" &&
                        transporterlist.Contains(g.AC1)
                    )
                    .Select(g => new SelectListItem
                    {
                        Value = g.Id.ToString(),
                        Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
                    })
                    .OrderBy(x => x.Text)
                    .ToList();

                // ✅ Add default item at index 0
                transporterAccounts.Insert(0, new SelectListItem
                {
                    Value = "",
                    Text = "-- Select Transporter Account --"
                });

                ViewBag.Transporters = transporterAccounts;

                var stationList = _context.AcPara
                    .Where(a => AccountCategories.Station.Contains(a.ActypeCode)
                                && a.Cocode == User.GetCompanyId().ToString()
                                && a.Parent == "P")
                    .Select(a => a.Accode)
                    .Distinct();

                var stationAccounts = _context.GLChart3
                    .Where(g =>
                        g.CoCode == User.GetCompanyId().ToString() &&
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

            var lastDoc = _context.ChallanHead
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