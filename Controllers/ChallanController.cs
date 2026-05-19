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

                foreach (var key in Request.Form.Keys)
                {
                    System.Diagnostics.Debug.WriteLine($"Form Key: {key} = {Request.Form[key]}");
                }
                System.Diagnostics.Debug.WriteLine($"Details Count: {model.Details?.Count ?? 0}");

                foreach (var d in model.Details ?? new List<ChallanDetailVM>())
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"BillTiNo={d.BillTiNo}, CusName={d.CusName}, Qty={d.Qty}");
                }
                // Initialize as the view-model type (fixed)
                model.Details ??= new List<ChallanDetailVM>();

                // Remove empty rows
                model.Details = model.Details
                    .Where(x =>
                        x.BillTiNo != null ||
                        !string.IsNullOrWhiteSpace(x.CusName) ||
                        !string.IsNullOrWhiteSpace(x.SendTo) ||
                        (x.Qty ?? 0) > 0 ||
                        (x.BillTiAmt ?? 0) > 0 ||
                        (x.PaidAmt ?? 0) > 0)
                    .ToList();

                // Check if details exist
                if (!model.Details.Any())
                {
                    ModelState.AddModelError("", "At least one Bilty detail is required.");
                }

                if (!ModelState.IsValid)
                {
                    LoadDropdowns();
                    return View(model);
                }

                using var transaction = _context.Database.BeginTransaction();

                // =========================
                // FILL HEAD AUTO VALUES
                // =========================
                model.Head.DocNo ??= GenerateDocNo();
                model.Head.CompanyId = User.GetCompanyId();
                model.Head.FyId = User.GetFinancialYearId();
                model.Head.CoCode = User.GetCompanyCode();

                model.Head.TotPaid = model.Details.Sum(x => x.PaidAmt ?? 0);

                model.Head.TotBillTi =
                     (model.Head.BillTiAmt ?? 0)
                     + model.Details.Sum(x => x.BillTiAmt ?? 0);

                model.Head.NetAmt =
                    (model.Head.TotToPaid ?? 0)
                    - (
                        (model.Head.PExpAmt ?? 0) +
                        (model.Head.PExpAmt2 ?? 0) +
                        (model.Head.PExpAmt3 ?? 0) +
                        (model.Head.LocalAmt ?? 0) +
                        (model.Head.TotOtherEx ?? 0) +
                        (model.Head.DeliveryAmt ?? 0) +
                        (model.Head.TotLifter2 ?? 0)
                      );

                // =========================
                // LOOKUP MASTER DATA
                // =========================
                var station = _context.GLChart3
                    .FirstOrDefault(x => x.Id == model.Head.StationId);

                var transporter = _context.GLChart3
                    .FirstOrDefault(x => x.Id == model.Head.TransId);

                model.Head.Station = station?.Name;
                model.Head.StationCode = station?.ACC;

                model.Head.Transporter = transporter?.Name;
                model.Head.TransCode = transporter?.ACC;

                // =========================
                // SAVE HEAD
                // =========================
                _context.ChallanHead.Add(model.Head);
                _context.SaveChanges();

                // =========================
                // SAVE DETAILS
                // Map ViewModel -> Entity before adding to DbContext (fixed)
                // =========================
                foreach (var item in model.Details)
                {                   
                    var det = new ChallanDet
                    {
                        ChallanHeadId = model.Head.Id,
                        DocNo = model.Head.DocNo,
                        DocDate = model.Head.DocDate,
                        CompanyId = model.Head.CompanyId,
                        FyId = model.Head.FyId,
                        CoCode = model.Head.CoCode,

                        Transporter = model.Head.TransCode,
                        VehicleNo = model.Head.VehicleNo,
                        TrName = model.Head.Transporter,

                        // Map fields from the view-model
                        CusName = item.CusName,
                        SendTo = item.SendTo,
                        BillTiNo = item.BillTiNo,
                        BillTiAmt = item.BillTiAmt,
                        PaidAmt = item.PaidAmt,
                        // Qty in entity is int? while VM uses decimal? - convert safely
                        Qty = item.Qty.HasValue ? (int?)Convert.ToInt32(item.Qty.Value) : null
                    };

                    _context.ChallanDet.Add(det);

                    // =========================
                    // UPDATE ORIGINAL BILTY TABLE
                    // Mark as used (Descyn = "Y")
                    // =========================
                    if (!string.IsNullOrWhiteSpace(item.DCNo))
                    {
                        // Find the original bilty record
                        var originalBilty = _context.IssHead // Replace with your actual table name
                            .FirstOrDefault(b => b.DocNo == item.DCNo && b.BillTiNo.ToString() == item.BillTiNo);

                        if (originalBilty != null)
                        {
                            originalBilty.DescYN = "Y";  // Mark as used
                            originalBilty.ChallanId = model.Head.Id; // Optional: store reference
                            _context.Entry(originalBilty).State = EntityState.Modified;
                        }
                    }
                }

                _context.SaveChanges();

                transaction.Commit();

                _audit.LogAsync(
                    "Create",
                    "Challan",
                    model.Head.Id.ToString(),
                    $"Challan Created: {model.Head.DocNo}"
                ).Wait();

                TempData["SuccessMessage"] = "✅ Challan Update successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _audit.LogAsync(
                    "Error",
                    "Challan Create",
                    "0",
                    ex.ToString()
                ).Wait();

                TempData["ErrorMessage"] = "❌ Failed to save challan.";

                LoadDropdowns();
                return View(model);
            }
        }

        public IActionResult Edit(int id)
        {
            LoadDropdowns();

            var head = _context.ChallanHead
                .FirstOrDefault(x => x.Id == id);

            if (head == null)
                return NotFound();

            var details = _context.ChallanDet
                 .Where(x => x.ChallanHeadId == id)
                 .Select(x => new ChallanDetailVM
                 {
                     CusName = x.CusName,
                     SendTo = x.SendTo,
                     BillTiNo = x.BillTiNo,
                     BillTiAmt = x.BillTiAmt,
                     PaidAmt = x.PaidAmt,
                     Qty = x.Qty
                 })
                 .ToList();

            var model = new ChallanViewModel
            {
                Head = head,
                Details = details ?? new List<ChallanDetailVM>()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(ChallanViewModel model)
        {
            try
            {
                model.Details ??= new List<ChallanDetailVM>();

                // Remove empty rows
                model.Details = model.Details
                    .Where(x =>
                        x.BillTiNo != null ||
                        !string.IsNullOrWhiteSpace(x.CusName) ||
                        (x.Qty ?? 0) > 0 ||
                        (x.BillTiAmt ?? 0) > 0)
                    .ToList();

                if (!ModelState.IsValid)
                {
                    LoadDropdowns();
                    return View(model);
                }

                using var transaction = _context.Database.BeginTransaction();

                var head = _context.ChallanHead
                    .Include(x => x.Details)
                    .FirstOrDefault(x => x.Id == model.Head.Id);

                if (head == null)
                    return NotFound();

                // =========================
                // UPDATE HEAD
                // =========================
                head.ChalNo = model.Head.ChalNo;
                head.DocDate = model.Head.DocDate;
                head.StationId = model.Head.StationId;
                head.TransId = model.Head.TransId;
                head.VehicleNo = model.Head.VehicleNo;
                head.Driver = model.Head.Driver;
                head.Narration = model.Head.Narration;

                head.PartyStationCode = model.Head.PartyStationCode;

                head.PExpCode = model.Head.PExpCode;
                head.PExpCode2 = model.Head.PExpCode2;
                head.PExpCode3 = model.Head.PExpCode3;

                head.PExpAmt = model.Head.PExpAmt;
                head.PExpAmt2 = model.Head.PExpAmt2;
                head.PExpAmt3 = model.Head.PExpAmt3;

                head.PExpBilti = model.Head.PExpBilti;
                head.PExpBilti2 = model.Head.PExpBilti2;
                head.PExpBilti3 = model.Head.PExpBilti3;

                head.PartyEx2 = model.Head.PartyEx2;
                head.LocalAmt2 = model.Head.LocalAmt2;
                head.OtherEx2 = model.Head.OtherEx2;

                head.LocalAmt = model.Head.LocalAmt;
                head.TotOtherEx = model.Head.TotOtherEx;

                head.TotToPaid = model.Head.TotToPaid;
                head.DeliveryAmt = model.Head.DeliveryAmt;
                head.BillTiAmt = model.Head.BillTiAmt;
                head.TotLifter2 = model.Head.TotLifter2;

                // =========================
                // RECALCULATE TOTALS
                // =========================
                head.TotBillTi =
                    (head.BillTiAmt ?? 0) +
                    model.Details.Sum(x => x.BillTiAmt ?? 0);

                head.NetAmt =
                    (head.TotToPaid ?? 0)
                    - (
                        (head.PExpAmt ?? 0) +
                        (head.PExpAmt2 ?? 0) +
                        (head.PExpAmt3 ?? 0) +
                        (head.LocalAmt ?? 0) +
                        (head.TotOtherEx ?? 0) +
                        (head.DeliveryAmt ?? 0) +
                        (head.TotLifter2 ?? 0)
                    );

                // =========================
                // DELETE OLD DETAILS
                // =========================
                _context.ChallanDet.RemoveRange(head.Details);
                _context.SaveChanges();

                // =========================
                // INSERT NEW DETAILS
                // =========================
                foreach (var d in model.Details)
                {
                    _context.ChallanDet.Add(new ChallanDet
                    {
                        ChallanHeadId = head.Id,
                        FyId = head.FyId,
                        CompanyId = head.CompanyId,
                        DocNo = head.DocNo,
                        DocDate = head.DocDate,
                        CoCode = head.CoCode,

                        BillTiNo = d.BillTiNo,
                        CusName = d.CusName,
                        SendTo = d.SendTo,
                        Qty = Convert.ToInt32(d.Qty),
                        BillTiAmt = d.BillTiAmt,
                        PaidAmt = d.PaidAmt,

                        VehicleNo = head.VehicleNo,
                        Transporter = head.Transporter,
                        TrName = head.Driver
                    });
                }

                _context.SaveChanges();

                transaction.Commit();

                TempData["SuccessMessage"] = "✅ Challan updated successfully.";

                return RedirectToAction(nameof(Index));
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

                //party station 
                var PartyStationList = _context.AcPara
                    .Where(a => AccountCategories.Party_Station.Contains(a.ActypeCode)
                                && a.Cocode == User.GetCompanyId().ToString()
                                && a.Parent == "P")
                    .Select(a => a.Accode)
                    .Distinct();

                var PartyStationAccounts = _context.GLChart3
                    .Where(g =>
                        g.CoCode == User.GetCompanyId().ToString() &&
                        g.AcType != "S" &&
                        PartyStationList.Contains(g.AC1)
                    )
                    .Select(g => new SelectListItem
                    {
                        Value = g.Id.ToString(),
                        Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
                    })
                    .OrderBy(x => x.Text)
                    .ToList();

                PartyStationAccounts.Insert(0, new SelectListItem
                {
                    Value = "",
                    Text = "-- Select Party Station Account --"
                });

                ViewBag.PartyStations = PartyStationAccounts;

                //party Exp
                var PartyExpList = _context.AcPara
                    .Where(a => AccountCategories.Too_PayParty.Contains(a.ActypeCode)
                                && a.Cocode == User.GetCompanyId().ToString()
                                && a.Parent == "P")
                    .Select(a => a.Accode)
                    .Distinct();

                var PartyExpAccounts = _context.GLChart3
                    .Where(g =>
                        g.CoCode == User.GetCompanyId().ToString() &&
                        g.AcType != "S" &&
                        PartyExpList.Contains(g.AC1)
                    )
                    .Select(g => new SelectListItem
                    {
                        Value = g.Id.ToString(),
                        Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
                    })
                    .OrderBy(x => x.Text)
                    .ToList();

                PartyExpAccounts.Insert(0, new SelectListItem
                {
                    Value = "",
                    Text = "-- Select Party Exp Account --"
                });

                ViewBag.PartyExps = PartyExpAccounts;


                //Load bilty
                ViewBag.BiltyList = _context.IssHead
    .Where(x => x.DescYN == "N" || string.IsNullOrEmpty(x.DescYN))
    .Select(x => new
    {
        x.Id,
        x.DocNo,
        x.DocDate,
        x.BillTiNo,
        x.BilNo,
        x.CusName,
        x.SendTo,
        x.Qty,
        x.PType,
        x.NetAmt,
        x.Labour,
        x.Cartage2,
        x.Cartage3,
        x.PartyEx,
        x.Lifter2,
        x.OtherEx
    })
    .ToList();
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