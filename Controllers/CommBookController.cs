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
using System.Reflection.Emit;

namespace Nskg.Controllers
{
    public class CommBookController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AccountingService _service;
        private readonly IAuditService _audit;

        public CommBookController(ApplicationDbContext context, AccountingService service, IAuditService audit)
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
        public IActionResult GetCommBookList()
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

                var query = _context.CommHead
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted);

                if (companyId > 0)
                {
                    query = query.Where(x => x.CompanyId == companyId);
                }

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
                        query = query.Where(x => x.DocDate.Date == targetDate);
                    }
                    else
                    {
                        query = query.Where(x => x.DocDate.ToString().Contains(val1));
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
                    docDate = b.DocDate != default ? b.DocDate.ToString("dd-MMM-yyyy") : "",
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
                _audit.LogAsync("Error", "CommBook", "0", ex.Message).Wait();
                int drawVal = 0;
                int.TryParse(Request.Form["draw"].FirstOrDefault(), out drawVal);
                return Json(new { draw = drawVal, recordsTotal = 0, recordsFiltered = 0, data = new List<object>() });
            }
        }

        public IActionResult Create()
        {
            LoadDropdowns();

            return View(new CommBookViewModel
            {
                Head = new CommHead
                {
                    DocDate = DateTime.Now,
                    DocNo = GenerateDocNo()
                },
                Details = new List<CommBookDetailVM>()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CommBookViewModel model)
        {
            try
            {
                // Debug output for posted form and model
                foreach (var key in Request.Form.Keys)
                {
                    System.Diagnostics.Debug.WriteLine($"Form Key: {key} = {Request.Form[key]}");
                }

                System.Diagnostics.Debug.WriteLine($"Details Count: {model.Details?.Count ?? 0}");

                foreach (var d in model.Details ?? new List<CommBookDetailVM>())
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"BillTiNo={d.BillTiNo}, CusName={d.CusName}, BillTiAmt={d.BillTiAmt}, PaidAmt={d.PaidAmt}");
                }

                // Ensure details list exists
                model.Details ??= new List<CommBookDetailVM>();

                // Remove empty rows (based on fields present in CommBookDetailVM)
                model.Details = model.Details
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x.BillTiNo) ||
                        !string.IsNullOrWhiteSpace(x.CusName) ||
                        !string.IsNullOrWhiteSpace(x.SendTo) ||
                        (x.BillTiAmt ?? 0) > 0 ||
                        (x.PaidAmt ?? 0) > 0 ||
                        (x.NetAmt ?? 0) > 0
                    )
                    .ToList();

                if (!model.Details.Any())
                {
                    ModelState.AddModelError("", "At least one commission detail is required.");
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

                // Populate common ERP fields - property names in CommHead
                model.Head.CompanyId = User.GetCompanyId();
                model.Head.FinancialYearId = User.GetFinancialYearId();

                model.Head.PaidAmt = model.Details.Sum(x => x.PaidAmt ?? 0);
                model.Head.BillTiAmt = model.Details.Sum(x => x.BillTiAmt ?? 0);

                // Recalculate totals if appropriate fields exist
                model.Head.TotAmt = (model.Head.TotAmt ?? 0) + model.Details.Sum(x => (x.NetAmt ?? 0));
                model.Head.NetAmt = (model.Head.TotNet ?? model.Head.TotAmt) ?? model.Head.TotAmt;

                model.Head.TotNet = model.Details.Sum(x => x.NetAmt ?? 0);
                model.Head.DeliveryAmt1 = model.Details.Sum(x => x.DeliveryAmt ?? 0);
                model.Head.DeliveryAmt2 = model.Details.Sum(x => x.ToPaidAmt ?? 0);

                model.Head.CreatedOn = DateTime.Now;
                model.Head.CreatedBy = User.Identity?.Name ?? string.Empty;

                // =========================
                // LOOKUP MASTER DATA
                // =========================
                var station = _context.GLChart3
                    .FirstOrDefault(x => x.Id == model.Head.StationId);

                var transporter = _context.GLChart3
                    .FirstOrDefault(x => x.Id == model.Head.TransId);

                var advance = _context.GLChart3
                    .FirstOrDefault(x => x.Id == model.Head.AdvanceId);

                model.Head.Station = station?.Name;
                model.Head.StationCode = station?.ACC;

                model.Head.Transporter = transporter?.Name;
                model.Head.TransCode = transporter?.ACC;

                model.Head.Advance = advance?.Name;
                model.Head.AdvanceCode = advance?.ACC;

                var salesAccount = _context.AcPara.Where(a => AccountCategories.Sales.Contains(a.ActypeCode)
                   && a.CompanyId == model.Head.CompanyId
                   && a.Parent == "P").Select(a => a.Accode).Distinct().FirstOrDefault();
                model.Head.AcCode = salesAccount;

                // =========================
                // SAVE HEAD
                // =========================
                _context.CommHead.Add(model.Head);
                _context.SaveChanges();

                // =========================
                // SAVE DETAILS
                // Map ViewModel -> Entity
                // =========================
                foreach (var item in model.Details)
                {
                    var det = new CommDetail
                    {
                        CommHeadId = model.Head.Id,
                        DocDate = model.Head.DocDate,
                        CompanyId = model.Head.CompanyId,
                        FinancialYearId = model.Head.FinancialYearId,
                        CreatedBy = model.Head.CreatedBy,
                        CreatedOn = model.Head.CreatedOn,

                        Fooder = item.Fooder,
                        CusName = item.CusName,
                        SendTo = item.SendTo,
                        BillTiAmt = item.BillTiAmt,
                        PaidAmt = item.PaidAmt,
                        ToPaidAmt = item.ToPaidAmt,
                        DeliveryAmt = item.DeliveryAmt,
                        LocalAmt = item.LocalAmt,
                        NetAmt = item.NetAmt
                    };

                    // Try to parse BillTiNo into int? (CommDetail uses int? BillTiNo)
                    if (!string.IsNullOrWhiteSpace(item.BillTiNo) &&
                        int.TryParse(item.BillTiNo, out var billTiInt))
                    {
                        det.BillTiNo = billTiInt;
                    }

                    _context.CommDetail.Add(det);

                    // =========================
                    // UPDATE ORIGINAL BILTY TABLE (IssHead)
                    // Mark as used (DescYN = "Y")
                    // =========================
                    if (!string.IsNullOrWhiteSpace(item.BillTiNo))
                    {
                        ChallanHead? originalBilty = null;

                        if (item.ChallanId.HasValue && item.ChallanId.Value > 0)
                        {
                            originalBilty = _context.ChallanHead.FirstOrDefault(b => b.Id == item.ChallanId.Value);
                        }

                        if (originalBilty == null && !string.IsNullOrWhiteSpace(item.DCNo))
                        {
                            originalBilty = _context.ChallanHead.FirstOrDefault(b => b.DocNo == item.DCNo.Trim());
                        }

                        if (originalBilty == null && int.TryParse(item.BillTiNo, out var billTiIntVal))
                        {
                            var candidates = _context.ChallanHead.Where(b => b.ChalNo == billTiIntVal).ToList();
                            originalBilty = candidates.FirstOrDefault(b =>
                                (string.IsNullOrEmpty(item.Fooder) || b.Station == item.Fooder || b.StationCode == item.FooderCode) &&
                                (string.IsNullOrEmpty(item.CusName) || b.Transporter == item.CusName) &&
                                (!item.NetAmt.HasValue || b.NetAmt == item.NetAmt)
                            ) ?? candidates.FirstOrDefault(b =>
                                (string.IsNullOrEmpty(item.Fooder) || b.Station == item.Fooder || b.StationCode == item.FooderCode) ||
                                (string.IsNullOrEmpty(item.CusName) || b.Transporter == item.CusName)
                            ) ?? candidates.FirstOrDefault();
                        }

                        if (originalBilty != null)
                        {
                            originalBilty.DescYn = "Y";
                            originalBilty.commBookId = model.Head.Id;
                            _context.Entry(originalBilty).State = EntityState.Modified;
                        }
                    }
                }

                _context.SaveChanges();

                var commDetails = _context.CommDetail.Where(x => x.CommHeadId == model.Head.Id).ToList();
                _service.PostCommBook(model.Head, commDetails);

                transaction.Commit();

                _audit.LogAsync(
                    "Create",
                    "CommBook",
                    model.Head.Id.ToString(),
                    $"Comm Book Created: {model.Head.DocNo}"
                ).Wait();

                TempData["SuccessMessage"] = "✅ Commission book saved successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _audit.LogAsync(
                    "Error",
                    "CommBook Create",
                    "0",
                    ex.ToString()
                ).Wait();

                TempData["ErrorMessage"] = "❌ Failed to save commission book.";

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

            var head = _context.CommHead
                .FirstOrDefault(x => x.Id == id);

            if (head == null)
                return NotFound();

            var details = _context.CommDetail
                 .Where(x => x.CommHeadId == id)
                 .Select(x => new CommBookDetailVM
                 {
                     BillTiNo = x.BillTiNo.HasValue ? x.BillTiNo.Value.ToString() : null,
                     Fooder = x.Fooder,
                     CusName = x.CusName,
                     SendTo = x.SendTo,
                     BillTiAmt = x.BillTiAmt,
                     PaidAmt = x.PaidAmt,
                     ToPaidAmt = x.ToPaidAmt,
                     DeliveryAmt = x.DeliveryAmt,
                     LocalAmt = x.LocalAmt,
                     NetAmt = x.NetAmt,
                     DCNo = x.DcNo,
                     FooderCode = x.FooderCode,
                     PartyEx = x.PartyExAmt,
                     Lifter2 = x.Lifter2Amt,
                     OtherEx = x.OtherExAmt
                 })
                 .ToList();

            var linkedChallans = _context.ChallanHead
                .Where(c => c.commBookId == id || (!string.IsNullOrEmpty(c.DocNo) && details.Select(d => d.DCNo).Contains(c.DocNo)))
                .ToList();

            var usedChallanIds = new HashSet<int>();
            foreach (var det in details)
            {
                int.TryParse(det.BillTiNo, out var chalNoInt);

                // Priority 1: Match within linkedChallans by DocNo
                var match = linkedChallans.FirstOrDefault(c => !usedChallanIds.Contains(c.Id) && !string.IsNullOrEmpty(det.DCNo) && c.DocNo == det.DCNo);

                // Priority 2: Match within linkedChallans by ChalNo, Station, Transporter, and Amounts
                if (match == null && chalNoInt > 0)
                {
                    match = linkedChallans.FirstOrDefault(c => !usedChallanIds.Contains(c.Id) &&
                        c.ChalNo == chalNoInt &&
                        (string.IsNullOrEmpty(det.Fooder) || c.Station == det.Fooder || c.StationCode == det.FooderCode) &&
                        (string.IsNullOrEmpty(det.CusName) || c.Transporter == det.CusName) &&
                        (!det.NetAmt.HasValue || c.NetAmt == det.NetAmt)
                    );
                }

                // Priority 3: Match within linkedChallans by ChalNo and Station/Transporter
                if (match == null && chalNoInt > 0)
                {
                    match = linkedChallans.FirstOrDefault(c => !usedChallanIds.Contains(c.Id) &&
                        c.ChalNo == chalNoInt &&
                        (string.IsNullOrEmpty(det.Fooder) || c.Station == det.Fooder || c.StationCode == det.FooderCode ||
                         string.IsNullOrEmpty(det.CusName) || c.Transporter == det.CusName)
                    );
                }

                // Priority 4: Match within linkedChallans by ChalNo only
                if (match == null && chalNoInt > 0)
                {
                    match = linkedChallans.FirstOrDefault(c => !usedChallanIds.Contains(c.Id) && c.ChalNo == chalNoInt);
                }

                // Priority 5: Fallback search across all Challans if not linked yet
                if (match == null && chalNoInt > 0)
                {
                    var candidates = _context.ChallanHead
                        .Where(c => c.ChalNo == chalNoInt)
                        .ToList();

                    match = candidates.FirstOrDefault(c =>
                        !usedChallanIds.Contains(c.Id) &&
                        (string.IsNullOrEmpty(det.Fooder) || c.Station == det.Fooder || c.StationCode == det.FooderCode) &&
                        (string.IsNullOrEmpty(det.CusName) || c.Transporter == det.CusName) &&
                        (!det.NetAmt.HasValue || c.NetAmt == det.NetAmt)
                    ) ?? candidates.FirstOrDefault(c =>
                        !usedChallanIds.Contains(c.Id) &&
                        (string.IsNullOrEmpty(det.Fooder) || c.Station == det.Fooder || c.StationCode == det.FooderCode ||
                         string.IsNullOrEmpty(det.CusName) || c.Transporter == det.CusName)
                    ) ?? candidates.FirstOrDefault(c => !usedChallanIds.Contains(c.Id));
                }

                if (match != null)
                {
                    det.ChallanId = match.Id;
                    if (string.IsNullOrEmpty(det.DCNo) && !string.IsNullOrEmpty(match.DocNo))
                    {
                        det.DCNo = match.DocNo;
                    }
                    usedChallanIds.Add(match.Id);
                }
            }

            var model = new CommBookViewModel
            {
                Head = head,
                Details = details ?? new List<CommBookDetailVM>()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(CommBookViewModel model)
        {
            try
            {
                model.Details ??= new List<CommBookDetailVM>();

                // Remove empty rows
                model.Details = model.Details
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x.BillTiNo) ||
                        !string.IsNullOrWhiteSpace(x.CusName) ||
                        !string.IsNullOrWhiteSpace(x.SendTo) ||
                        (x.BillTiAmt ?? 0) > 0 ||
                        (x.PaidAmt ?? 0) > 0 ||
                        (x.NetAmt ?? 0) > 0)
                    .ToList();

                if (!model.Details.Any())
                {
                    ModelState.AddModelError("", "At least one commission detail is required.");
                }

                if (!ModelState.IsValid)
                {
                    LoadDropdowns();
                    return View(model);
                }

                using var transaction = _context.Database.BeginTransaction();

                var head = _context.CommHead
                    .Include(x => x.Details)
                    .FirstOrDefault(x => x.Id == model.Head.Id);

                if (head == null)
                    return NotFound();

                // Unlink previously linked challans that are NOT present in the updated details
                var previouslyLinked = _context.ChallanHead
                    .Where(ch => ch.commBookId == head.Id)
                    .ToList();

                // Collect BillTiNo values from submitted details for comparison
                var newBillNos = model.Details
                    .Where(d => !string.IsNullOrWhiteSpace(d.BillTiNo))
                    .Select(d => d.BillTiNo!.Trim())
                    .ToList();

                foreach (var ch in previouslyLinked)
                {
                    var stillLinked = false;

                    // If challan has a DocNo, check string match
                    if (!string.IsNullOrWhiteSpace(ch.DocNo))
                    {
                        if (newBillNos.Any(nb => string.Equals(nb, ch.DocNo, StringComparison.OrdinalIgnoreCase)))
                            stillLinked = true;
                    }

                    // If challan has a numeric ChalNo, check numeric match
                    if (!stillLinked && ch.ChalNo != null)
                    {
                        foreach (var nb in newBillNos)
                        {
                            if (decimal.TryParse(nb, out var nbDec) && ch.ChalNo == nbDec)
                            {
                                stillLinked = true;
                                break;
                            }
                        }
                    }

                    if (!stillLinked)
                    {
                        ch.DescYn = "N";
                        ch.commBookId = null;
                        ch.ModifiedOn = DateTime.Now;
                        ch.ModifiedBy = User?.Identity?.Name;
                        _context.Entry(ch).State = EntityState.Modified;
                    }
                }

                // Update head fields
                head.ChalNo = model.Head.ChalNo;
                head.DocDate = model.Head.DocDate;
                head.StationId = model.Head.StationId;
                head.TransId = model.Head.TransId;
                head.VehicleNo = model.Head.VehicleNo;
                head.Driver = model.Head.Driver;
                head.Narration = model.Head.Narration;

                head.StationAmt = model.Head.StationAmt;
                head.TransporterAmt = model.Head.TransporterAmt;
                head.AdvanceId = model.Head.AdvanceId;
                head.AdvanceAmt = model.Head.AdvanceAmt;

                head.PartyExAmt = model.Head.PartyExAmt;
                head.Lifter2Amt = model.Head.Lifter2Amt;
                head.OtherExAmt = model.Head.OtherExAmt;

                head.DeliveryAmt = model.Head.DeliveryAmt;
                head.LocalAmt = model.Head.LocalAmt;
                head.Labour = model.Head.Labour;
                head.Tax = model.Head.Tax;

                head.TotNet = model.Details.Sum(x => x.NetAmt ?? 0);
                head.BillTiAmt = model.Details.Sum(x => x.BillTiAmt ?? 0);
                head.PaidAmt = model.Details.Sum(x => x.PaidAmt ?? 0);
                head.TotAmt = (model.Head.TotAmt ?? 0) + head.TotNet;

                if (string.IsNullOrEmpty(head.AcCode))
                {
                    var salesAccount = _context.AcPara.Where(a => AccountCategories.Sales.Contains(a.ActypeCode)
                       && a.CompanyId == head.CompanyId
                       && a.Parent == "P").Select(a => a.Accode).Distinct().FirstOrDefault();
                    head.AcCode = salesAccount;
                }

                head.ModifiedOn = DateTime.Now;
                head.ModifiedBy = User?.Identity?.Name;

                // Delete old details
                var oldDetails = _context.CommDetail.Where(d => d.CommHeadId == head.Id).ToList();
                _context.CommDetail.RemoveRange(oldDetails);
                _context.SaveChanges();

                // Insert new details and link challans
                foreach (var item in model.Details)
                {
                    var det = new CommDetail
                    {
                        CommHeadId = head.Id,
                        DocDate = head.DocDate,
                        CompanyId = head.CompanyId,
                        FinancialYearId = head.FinancialYearId,
                        CreatedBy = head.CreatedBy,
                        CreatedOn = head.CreatedOn,

                        Fooder = item.Fooder,
                        CusName = item.CusName,
                        SendTo = item.SendTo,
                        BillTiAmt = item.BillTiAmt,
                        PaidAmt = item.PaidAmt,
                        ToPaidAmt = item.ToPaidAmt,
                        DeliveryAmt = item.DeliveryAmt,
                        LocalAmt = item.LocalAmt,
                        NetAmt = item.NetAmt,
                        DcNo = item.DCNo
                    };

                    if (!string.IsNullOrWhiteSpace(item.BillTiNo) && int.TryParse(item.BillTiNo, out var billTiInt))
                        det.BillTiNo = billTiInt;

                    _context.CommDetail.Add(det);

                    // Link to ChallanHead if BillTiNo provided
                    if (!string.IsNullOrWhiteSpace(item.BillTiNo))
                    {
                        ChallanHead? chall = null;
                        if (item.ChallanId.HasValue && item.ChallanId.Value > 0)
                        {
                            chall = _context.ChallanHead.FirstOrDefault(c => c.Id == item.ChallanId.Value);
                        }

                        if (chall == null && !string.IsNullOrWhiteSpace(item.DCNo))
                        {
                            chall = _context.ChallanHead.FirstOrDefault(c => c.DocNo == item.DCNo.Trim());
                        }

                        if (chall == null && int.TryParse(item.BillTiNo, out var asInt))
                        {
                            var candidates = _context.ChallanHead.Where(c => c.ChalNo == asInt).ToList();
                            chall = candidates.FirstOrDefault(c =>
                                (string.IsNullOrEmpty(item.Fooder) || c.Station == item.Fooder || c.StationCode == item.FooderCode) &&
                                (string.IsNullOrEmpty(item.CusName) || c.Transporter == item.CusName) &&
                                (!item.NetAmt.HasValue || c.NetAmt == item.NetAmt)
                            ) ?? candidates.FirstOrDefault(c =>
                                (string.IsNullOrEmpty(item.Fooder) || c.Station == item.Fooder || c.StationCode == item.FooderCode) ||
                                (string.IsNullOrEmpty(item.CusName) || c.Transporter == item.CusName)
                            ) ?? candidates.FirstOrDefault();
                        }

                        if (chall != null)
                        {
                            chall.DescYn = "Y";
                            chall.commBookId = head.Id;
                            chall.ModifiedOn = DateTime.Now;
                            chall.ModifiedBy = User?.Identity?.Name;
                            _context.Entry(chall).State = EntityState.Modified;
                        }
                    }
                }

                _context.SaveChanges();

                var updatedCommDetails = _context.CommDetail.Where(x => x.CommHeadId == head.Id).ToList();
                _service.PostCommBook(head, updatedCommDetails);

                transaction.Commit();

                TempData["SuccessMessage"] = "✅ Commission book updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _audit.LogAsync("Error", "CommBook Edit", "0", ex.ToString()).Wait();
                TempData["ErrorMessage"] = "❌ Failed to update commission book.";
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
                var v = _context.CommHead
                    .Include(x => x.Details)
                    .FirstOrDefault(x => x.Id == id);

                if (v == null)
                {
                    return Json(new { success = false, message = "CommBook not found!" });
                }

                // Remove related GL transactions
                var gl = _context.GLTrans
                    .Where(x => x.RefId == v.Id && (x.RefType == "CB" || x.RefType == "TR" || x.RefType == "LB" || x.RefType == "MU"));

                _context.GLTrans.RemoveRange(gl);

                // Before soft-deleting CommHead, unlink related ChallanHead entries
                var linkedChallans = _context.ChallanHead
                    .Where(ch => ch.commBookId == v.Id)
                    .ToList();

                foreach (var ch in linkedChallans)
                {
                    ch.DescYn = "N"; // mark challan as not described/available
                    ch.commBookId = null;
                    ch.ModifiedOn = DateTime.Now;
                    ch.ModifiedBy = User?.Identity?.Name;
                    _context.Entry(ch).State = EntityState.Modified;
                }

                // Soft-delete CommHead and mark details as deleted (DescYn)
                v.IsDeleted = true;
                v.ModifiedOn = DateTime.Now;
                v.ModifiedBy = User?.Identity?.Name;

                //foreach (var det in v.Details)
                //{
                //    det.DescYn = true;
                //}

                _context.Entry(v).State = EntityState.Modified;
                _context.SaveChanges();

                _audit.LogAsync("Delete", "CommBook", id.ToString(),
                    $"Soft-deleted: {v.DocNo}").Wait();

                return Json(new { success = true, message = "CommBook soft-deleted successfully!" });
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

                var advancelist = _context.AcPara
    .Where(a => AccountCategories.Advance.Contains(a.ActypeCode)
                && a.CompanyId == User.GetCompanyId()
                && a.Parent == "P")
    .Select(a => a.Accode)
    .Distinct();

                var advanceAccounts = _context.GLChart3
                    .Where(g =>
                        g.CompanyId == User.GetCompanyId() &&
                        g.AcType != "S" &&
                        advancelist.Contains(g.AC1)
                    )
                    .Select(g => new SelectListItem
                    {
                        Value = g.Id.ToString(),
                        Text = g.Name + " (" + (g.AC1 + g.AC3) + ")"
                    })
                    .OrderBy(x => x.Text)
                    .ToList();

                // ✅ Add default item at index 0
                advanceAccounts.Insert(0, new SelectListItem
                {
                    Value = "",
                    Text = "-- Select Advance Account --"
                });

                ViewBag.Advances = advanceAccounts;

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


                //Load bilty
                ViewBag.ChallanList = _context.ChallanHead
    .Where(x => x.DescYn == "N" || string.IsNullOrEmpty(x.DescYn))
    .Select(x => new
    {
        x.Id,
        x.DocNo,
        x.DocDate,
        x.ChalNo,
        x.VehicleNo,
        x.Transporter,
        x.Driver,
        x.StationCode,
        x.Station,
        x.TotPaid,
        x.NetAmt,
        x.TotToPaid,
        x.TotBillTi,
        x.DeliveryAmt,
        x.LocalAmt,
        x.TotPartyEx,
        x.TotLifter2,
        x.TotOtherEx

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

            var lastDoc = _context.CommHead
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
