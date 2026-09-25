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
            return View();
        }

        [HttpPost]
        public IActionResult GetChallanList()
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

                var query = _context.ChallanHead
                    .AsNoTracking();

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrWhiteSpace(searchValue))
                {
                    searchValue = searchValue.Trim().ToLower();
                    query = query.Where(x =>
                        (x.DocNo != null && x.DocNo.ToLower().Contains(searchValue)) ||
                        (x.Station != null && x.Station.ToLower().Contains(searchValue)) ||
                        (x.Transporter != null && x.Transporter.ToLower().Contains(searchValue))
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
                    query = query.Where(x => x.DocNo != null && x.DocNo.ToLower().Contains(val0));
                }

                if (!string.IsNullOrWhiteSpace(col1Search))
                {
                    var val1 = col1Search.Trim();
                    if (DateTime.TryParse(val1, out var parsedDate))
                    {
                        var targetDate = parsedDate.Date;
                        query = query.Where(x => x.DocDate.HasValue && x.DocDate.Value.Date == targetDate);
                    }
                    else
                    {
                        query = query.Where(x => x.DocDate.HasValue && x.DocDate.Value.ToString().Contains(val1));
                    }
                }

                if (!string.IsNullOrWhiteSpace(col2Search))
                {
                    var val2 = col2Search.Trim().ToLower();
                    query = query.Where(x => x.Station != null && x.Station.ToLower().Contains(val2));
                }

                if (!string.IsNullOrWhiteSpace(col3Search))
                {
                    var val3 = col3Search.Trim().ToLower();
                    query = query.Where(x => x.Transporter != null && x.Transporter.ToLower().Contains(val3));
                }

                int filterRecords = query.Count();

                // Sorting
                switch (sortColumnIndex)
                {
                    case "0":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.DocNo) : query.OrderByDescending(x => x.DocNo);
                        break;
                    case "1":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.DocDate).ThenBy(x => x.Id) : query.OrderByDescending(x => x.DocDate).ThenByDescending(x => x.Id);
                        break;
                    case "2":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.Station) : query.OrderByDescending(x => x.Station);
                        break;
                    case "3":
                        query = sortColumnDir == "asc" ? query.OrderBy(x => x.Transporter) : query.OrderByDescending(x => x.Transporter);
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
                        b.DocDate,
                        b.Station,
                        b.Transporter
                    })
                    .ToList();

                var data = rawData.Select(b => new
                {
                    id = b.Id,
                    docNo = b.DocNo ?? "",
                    docDate = b.DocDate.HasValue ? b.DocDate.Value.ToString("dd-MMM-yyyy") : "",
                    station = b.Station ?? "",
                    transporter = b.Transporter ?? ""
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
                _audit.LogAsync("Error", "Challan", "0", ex.Message).Wait();
                int drawVal = 0;
                int.TryParse(Request.Form["draw"].FirstOrDefault(), out drawVal);
                return Json(new { draw = drawVal, recordsTotal = 0, recordsFiltered = 0, data = new List<object>() });
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

                //foreach (var key in Request.Form.Keys)
                //{
                //    System.Diagnostics.Debug.WriteLine($"Form Key: {key} = {Request.Form[key]}");
                //}
                //System.Diagnostics.Debug.WriteLine($"Details Count: {model.Details?.Count ?? 0}");

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

                // Allow saving even when there are no detail rows.
                // Remove any ModelState entries that belong to the Details collection so
                // validation for missing/malformed detail items does not block saving the head.
                var detailKeys = ModelState.Keys
                    .Where(k => !string.IsNullOrEmpty(k) && (k.StartsWith("Details") || k.Contains("Details[")))
                    .ToList();

                foreach (var key in detailKeys)
                {
                    ModelState.Remove(key);
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
                            originalBilty.VehicleNo = model.Head.VehicleNo;
                            _context.Entry(originalBilty).State = EntityState.Modified;
                        }
                    }
                }

                _context.SaveChanges();

                var challanDetails = _context.ChallanDet.Where(x => x.ChallanHeadId == model.Head.Id).ToList();
                _service.PostChallan(model.Head, challanDetails);

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

        [HttpGet]
        public IActionResult GetChallanId(
            string? docNo,
            string? chalNo,
            string? station,
            string? stationCode,
            string? transporter,
            string? driver,
            decimal? netAmt,
            decimal? billTiAmt,
            decimal? paidAmt,
            decimal? toPaidAmt,
            decimal? deliveryAmt,
            decimal? localAmt,
            long? commBookId)
        {
            try
            {
                // Priority 1: Match by exact DocNo
                if (!string.IsNullOrWhiteSpace(docNo))
                {
                    var c = _context.ChallanHead.AsNoTracking().FirstOrDefault(x => x.DocNo == docNo.Trim());
                    if (c != null) return Json(new { success = true, id = c.Id });
                }

                int? chalNoInt = null;
                if (!string.IsNullOrWhiteSpace(chalNo) && int.TryParse(chalNo.Trim(), out var parsedInt))
                {
                    chalNoInt = parsedInt;
                }

                if (chalNoInt.HasValue)
                {
                    // Candidate challans by ChalNo
                    var query = _context.ChallanHead.AsNoTracking().Where(x => x.ChalNo == chalNoInt.Value);

                    // If commBookId provided, check if any matched this commBook
                    if (commBookId.HasValue && commBookId.Value > 0)
                    {
                        var commMatch = query.FirstOrDefault(x => x.commBookId == commBookId.Value);
                        if (commMatch != null) return Json(new { success = true, id = commMatch.Id });
                    }

                    var candidates = query.ToList();

                    if (candidates.Count == 1)
                    {
                        return Json(new { success = true, id = candidates[0].Id });
                    }

                    if (candidates.Count > 1)
                    {
                        // Match with all attributes: Station, Transporter, and Amounts
                        var bestMatch = candidates.FirstOrDefault(c =>
                            (string.IsNullOrWhiteSpace(station) || string.Equals(c.Station?.Trim(), station.Trim(), StringComparison.OrdinalIgnoreCase) || string.Equals(c.StationCode?.Trim(), stationCode?.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                            (string.IsNullOrWhiteSpace(transporter) || string.Equals(c.Transporter?.Trim(), transporter.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                            (!netAmt.HasValue || c.NetAmt == netAmt) &&
                            (!billTiAmt.HasValue || c.TotBillTi == billTiAmt)
                        );

                        if (bestMatch != null) return Json(new { success = true, id = bestMatch.Id });

                        // Match by Station and Transporter
                        bestMatch = candidates.FirstOrDefault(c =>
                            (string.IsNullOrWhiteSpace(station) || string.Equals(c.Station?.Trim(), station.Trim(), StringComparison.OrdinalIgnoreCase) || string.Equals(c.StationCode?.Trim(), stationCode?.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                            (string.IsNullOrWhiteSpace(transporter) || string.Equals(c.Transporter?.Trim(), transporter.Trim(), StringComparison.OrdinalIgnoreCase))
                        );

                        if (bestMatch != null) return Json(new { success = true, id = bestMatch.Id });

                        // Match by Station or Transporter and NetAmt
                        bestMatch = candidates.FirstOrDefault(c =>
                            (string.IsNullOrWhiteSpace(station) || string.Equals(c.Station?.Trim(), station.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                            (string.IsNullOrWhiteSpace(transporter) || string.Equals(c.Transporter?.Trim(), transporter.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                            (netAmt.HasValue && c.NetAmt == netAmt)
                        );

                        if (bestMatch != null) return Json(new { success = true, id = bestMatch.Id });

                        return Json(new { success = true, id = candidates[0].Id });
                    }
                }

                return Json(new { success = false, message = "Challan not found" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
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
                     DCNo = x.DCNo,
                     BilNo = x.BilNo,
                     BillTiNo = x.BillTiNo,
                     BillTiAmt = x.BillTiAmt,
                     PaidAmt = x.PaidAmt,
                     Qty = x.Qty
                 })
                 .ToList();

            var dcNos = details.Where(d => !string.IsNullOrEmpty(d.DCNo)).Select(d => d.DCNo).Distinct().ToList();
            var biltyMap = _context.IssHead
                .Where(b => !b.IsDeleted && (b.ChallanId == id || (b.DocNo != null && dcNos.Contains(b.DocNo))))
                .Select(b => new { b.Id, b.DocNo, b.BillTiNo })
                .ToList();

            foreach (var det in details)
            {
                var match = biltyMap.FirstOrDefault(b => (!string.IsNullOrEmpty(det.DCNo) && b.DocNo == det.DCNo)
                    || (det.BillTiNo != null && b.BillTiNo.HasValue && b.BillTiNo.Value.ToString() == det.BillTiNo));
                if (match != null)
                {
                    det.BiltyId = match.Id;
                }
            }

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
                    .Where(x => !string.IsNullOrWhiteSpace(x.BillTiNo) ||
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
                head.TotPaid = model.Details.Sum(x => x.PaidAmt ?? 0);

                // =========================
                // RECALCULATE TOTALS
                // =========================
                head.TotBillTi = (head.BillTiAmt ?? 0) + model.Details.Sum(x => x.BillTiAmt ?? 0);

                head.NetAmt = (head.TotToPaid ?? 0) - (
                    (head.PExpAmt ?? 0) +
                    (head.PExpAmt2 ?? 0) +
                    (head.PExpAmt3 ?? 0) +
                    (head.LocalAmt ?? 0) +
                    (head.TotOtherEx ?? 0) +
                    (head.DeliveryAmt ?? 0) +
                    (head.TotLifter2 ?? 0)
                );

                // =========================
                // GET CURRENT DC NUMBERS
                // =========================
                var currentDcNos = model.Details
                    .Where(x => !string.IsNullOrWhiteSpace(x.DCNo))
                    .Select(x => x.DCNo.Trim())
                    .ToList();

                // =========================
                // UNLINK REMOVED BILTIES
                // =========================
                var linkedBilties = _context.IssHead
                    .Where(i => i.ChallanId == head.Id)
                    .ToList();

                foreach (var bilty in linkedBilties)
                {
                    if (!currentDcNos.Contains(bilty.DocNo))
                    {
                        bilty.DescYN = "N";
                        bilty.VehicleNo = null;
                        bilty.ChallanId = null;
                        _context.Entry(bilty).State = EntityState.Modified;
                    }
                }

                // =========================
                // REMOVE OLD DETAILS
                // =========================
                _context.ChallanDet.RemoveRange(head.Details);
                _context.SaveChanges();

                // =========================
                // ADD NEW DETAILS
                // =========================
                foreach (var detail in model.Details)
                {
                    var challanDet = new ChallanDet
                    {
                        ChallanHeadId = head.Id,
                        FyId = head.FyId,
                        CompanyId = head.CompanyId,
                        DocNo = head.DocNo,
                        DocDate = head.DocDate,
                        CoCode = head.CoCode,
                        BillTiNo = detail.BillTiNo,
                        CusName = detail.CusName,
                        SendTo = detail.SendTo,
                        Qty = detail.Qty.HasValue ? Convert.ToInt32(detail.Qty.Value) : (int?)null,
                        BillTiAmt = detail.BillTiAmt,
                        PaidAmt = detail.PaidAmt,
                        VehicleNo = head.VehicleNo,
                        Transporter = head.Transporter,
                        TrName = head.Driver,
                        DCNo = detail.DCNo
                    };

                    _context.ChallanDet.Add(challanDet);

                    // =========================
                    // LINK BILTY TO CHALLAN
                    // =========================
                    if (!string.IsNullOrWhiteSpace(detail.DCNo))
                    {
                        var bilty = _context.IssHead
                            .FirstOrDefault(b => b.DocNo == detail.DCNo);

                        if (bilty != null)
                        {
                            bilty.DescYN = "Y";
                            bilty.ChallanId = head.Id;
                            bilty.VehicleNo = head.VehicleNo;
                            _context.Entry(bilty).State = EntityState.Modified;
                        }
                    }
                }

                _context.SaveChanges();

                var updatedDetails = _context.ChallanDet.Where(x => x.ChallanHeadId == head.Id).ToList();
                _service.PostChallan(head, updatedDetails);

                transaction.Commit();

                _audit.LogAsync("Edit", "Challan", head.Id.ToString(),
                    $"Challan Updated: {head.DocNo}").Wait();

                TempData["SuccessMessage"] = "✅ Challan updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "Challan Edit", model.Head.Id.ToString(), ex.Message).Wait();
                TempData["ErrorMessage"] = $"❌ Error: {ex.Message}";
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

                // Remove related GL transactions
                var gl = _context.GLTrans
                    .Where(x => x.RefId == v.Id && x.RefType == "CL");

                _context.GLTrans.RemoveRange(gl);

                // Soft-delete: mark head.IsDeleted and mark each detail DescYn = "Y"
                v.IsDeleted = true;
                v.ModifiedOn = DateTime.Now;
                v.ModifiedBy = User?.Identity?.Name;

                //foreach (var det in v.Details)
                //{
                //    det.DescYn = "Y"; // mark detail as deleted
                //}

                // Also, for any IssHead rows that were linked, clear ChallanId? This keeps history but marks bilty command as used.
                var iss = _context.IssHead.Where(i => i.ChallanId == v.Id).ToList();
                foreach (var item in iss)
                {
                    // When challan deleted we may want to unmark original bilty as unused
                    item.DescYN = "N";
                    item.ChallanId = null;
                    item.VehicleNo = null;
                    _context.Entry(item).State = EntityState.Modified;
                }

                _context.Entry(v).State = EntityState.Modified;
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
                && a.CompanyId == User.GetCompanyId()
                && a.Parent == "P")
    .Select(a => a.Accode)
    .Distinct();

                var transporterAccounts = _context.GLChart3
                    .Where(g =>
                        g.CompanyId == User.GetCompanyId() &&
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

                //party station 
                var PartyStationList = _context.AcPara
                    .Where(a => AccountCategories.Party_Station.Contains(a.ActypeCode)
                                && a.CompanyId == User.GetCompanyId()
                                && a.Parent == "P")
                    .Select(a => a.Accode)
                    .Distinct();

                var PartyStationAccounts = _context.GLChart3
                    .Where(g =>
                        g.CompanyId == User.GetCompanyId() &&
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
                                && a.CompanyId == User.GetCompanyId()
                                && a.Parent == "P")
                    .Select(a => a.Accode)
                    .Distinct();

                var PartyExpAccounts = _context.GLChart3
                    .Where(g =>
                        g.CompanyId == User.GetCompanyId() &&
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


                // Load bilty - filter by user CompanyId, 3+ days old bilties first at top, then order by DocDate descending
                var userCompanyId = User.GetCompanyId();
                var threeDaysAgo = DateTime.Today.AddDays(-3);
                ViewBag.BiltyList = _context.IssHead
                    .Where(x => (x.DescYN == "N" || string.IsNullOrEmpty(x.DescYN)) && !x.IsDeleted && (userCompanyId == 0 || x.CompanyId == userCompanyId))
                    .Select(x => new
                    {
                        x.Id,
                        x.CompanyId,
                        x.StationId,
                        x.SCode,
                        x.Fooder,
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
                    .AsEnumerable()
                    .OrderBy(x => x.DocDate.HasValue && x.DocDate.Value.Date <= threeDaysAgo ? 0 : 1)
                    .ThenByDescending(x => x.DocDate)
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