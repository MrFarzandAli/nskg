using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nskg.Data;
using Nskg.Extensions;
using Nskg.Models;
using Nskg.Repositories.Interfaces;
using Nskg.Services;

namespace Nskg.Controllers
{
    [Authorize(Roles = "Admin")]
    public class FinancialYearController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditService _audit;
        private readonly FinancialYearClosingService _financialYearClosingService;
        private readonly ApplicationDbContext _context;

        public FinancialYearController(
            IUnitOfWork unitOfWork,
            IAuditService audit,
            FinancialYearClosingService financialYearClosingService,
            ApplicationDbContext context)
        {
            _unitOfWork = unitOfWork;
            _audit = audit;
            _financialYearClosingService = financialYearClosingService;
            _context = context;
        }

        private string GetUser()
        {
            return User?.Identity?.Name ?? "System";
        }

        public async Task<IActionResult> Index()
        {
            var years = (await _unitOfWork.FinancialYearRepository.GetAllWithCompanyAsync())
                        .Where(x => !x.IsDeleted);

            return View(years);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Companies = new SelectList(
                (await _unitOfWork.Companies.GetAllAsync()).Where(x => !x.IsDeleted),
                "Id", "Name");

            return View();
        }

        // ✅ CREATE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FinancialYear model)
        {
            try
            {
                model.YearName = $"{model.StartDate:yyyy}-{model.EndDate:yyyy}";

                // 🔥 AUDIT
                model.CreatedOn = DateTime.Now;
                model.CreatedBy = GetUser();
                model.IsDeleted = false;

                await _unitOfWork.FinancialYears.AddAsync(model);
                await _unitOfWork.SaveAsync();

                await _audit.LogAsync(
                    "Create",
                    "FinancialYears",
                    model.Id.ToString(),
                    $"Created Financial Year: {model.YearName}"
                );

                TempData["SuccessMessage"] = "✨ Financial Year created successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "FinancialYears", "0", ex.Message);

                TempData["ErrorMessage"] = "❌ Failed to create Financial Year!";
                return View(model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            var data = await _unitOfWork.FinancialYears.GetByIdAsync(id);

            if (data == null || data.IsDeleted)
            {
                TempData["ErrorMessage"] = "Financial Year not found!";
                return RedirectToAction("Index");
            }

            ViewBag.Companies = new SelectList(
                (await _unitOfWork.Companies.GetAllAsync()).Where(x => !x.IsDeleted),
                "Id", "Name", data.CompanyId);

            return View(data);
        }

        // ✅ UPDATE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(FinancialYear model)
        {
            try
            {
                var existing = await _unitOfWork.FinancialYears.GetByIdAsync(model.Id);

                if (existing == null)
                    return NotFound();

                // 🔥 UPDATE FIELDS
                existing.CompanyId = model.CompanyId;
                existing.StartDate = model.StartDate;
                existing.EndDate = model.EndDate;
                existing.YearName = $"{model.StartDate:yyyy}-{model.EndDate:yyyy}";
                existing.IsClosed = model.IsClosed;

                // 🔥 AUDIT
                existing.ModifiedOn = DateTime.Now;
                existing.ModifiedBy = GetUser();

                _unitOfWork.FinancialYears.Update(existing);
                await _unitOfWork.SaveAsync();

                await _audit.LogAsync(
                    "Update",
                    "FinancialYears",
                    model.Id.ToString(),
                    $"Updated Financial Year: {existing.YearName}"
                );

                TempData["InfoMessage"] = "✏️ Financial Year updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "FinancialYears", model.Id.ToString(), ex.Message);

                TempData["ErrorMessage"] = "❌ Failed to update Financial Year!";
                return View(model);
            }
        }

        // ✅ SOFT DELETE
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var data = await _unitOfWork.FinancialYears.GetByIdAsync(id);

                if (data == null)
                {
                    return Json(new { success = false, message = "Financial Year not found!" });
                }

                // 🔥 SOFT DELETE
                data.IsDeleted = true;
                data.ModifiedOn = DateTime.Now;
                data.ModifiedBy = GetUser();

                _unitOfWork.FinancialYears.Update(data);
                await _unitOfWork.SaveAsync();

                await _audit.LogAsync(
                    "Delete",
                    "FinancialYears",
                    id.ToString(),
                    $"Soft Deleted Financial Year: {data.YearName}"
                );

                return Json(new { success = true, message = "Financial Year deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "FinancialYears", id.ToString(), ex.Message);

                return Json(new { success = false, message = ex.Message });
            }
        }

        // 🔥 CLOSE YEAR (No change needed, but safe check added)
        public IActionResult CloseYear()
        {
            var list = _context.FinancialYears
                .Where(x => x.CompanyId == User.GetCompanyId() && !x.IsDeleted)
                .OrderByDescending(x => x.StartDate)
                .ToList();

            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> CloseYear(int fyId)
        {
            var list = _context.FinancialYears
              .Where(x => x.CompanyId == User.GetCompanyId() && !x.IsDeleted)
              .OrderByDescending(x => x.StartDate)
              .ToList();

            var fy = await _unitOfWork.FinancialYears.GetByIdAsync(fyId);

            if (fy == null || fy.IsDeleted)
            {
                TempData["ErrorMessage"] = "❌ Financial year already closed!";
                return View(list);
                //            throw new Exception("Financial year not found.");
            }

            if (fy.IsClosed)
            {
                //throw new Exception("Financial year already closed.");
                TempData["ErrorMessage"] = "❌ Financial year already closed!";
                return View(list);
            }

            if (fy.Id == User.GetFinancialYearId())
            {
                TempData["ErrorMessage"] = "Cannot close active financial year.";
                return View(list);
            }
                //throw new Exception("Cannot close active financial year.");

            var nextYear = _context.FinancialYears
                .FirstOrDefault(x => x.StartDate > fy.StartDate && !x.IsDeleted);

            if (nextYear == null)
            {
                TempData["ErrorMessage"] = "Next financial year does not exist.";
                return View(list);
            }
                //throw new Exception("Next financial year does not exist.");

            var chart = _context.GLChart3.FirstOrDefault(x => x.AcType == "C");

            await _financialYearClosingService.CloseFinancialYearAsync(
                companyId: User.GetCompanyId(),
                closingFinancialYearId: fyId,
                retainedEarningsAccode: chart.ACC
            );

            TempData["Success"] = "Financial Year Closed Successfully.";

            return View(list);
        }
    }
}