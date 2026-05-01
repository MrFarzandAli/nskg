using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nskg.Models;
using Nskg.Repositories.Interfaces;
using System.Security.Claims;

namespace Nskg.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CompanyController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditService _audit;

        public CompanyController(IUnitOfWork unitOfWork, IAuditService audit)
        {
            _unitOfWork = unitOfWork;
            _audit = audit;
        }

        private string GetUser()
        {
            return User?.Identity?.Name ?? "System";
        }

        public async Task<IActionResult> Index()
        {
            // ❗ Only show non-deleted records
            var companies = (await _unitOfWork.Companies.GetAllAsync())
                            .Where(x => !x.IsDeleted);

            return View(companies);
        }

        public IActionResult Create()
        {
            return View();
        }

        // ✅ CREATE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Company model)
        {
            try
            {
                // 🔥 SET AUDIT FIELDS
                model.CreatedOn = DateTime.Now;
                model.CreatedBy = GetUser();
                model.IsDeleted = false;

                await _unitOfWork.Companies.AddAsync(model);
                await _unitOfWork.SaveAsync();

                await _audit.LogAsync(
                    "Create",
                    "Company",
                    model.Id.ToString(),
                    $"Created Company: {model.Name}"
                );

                TempData["SuccessMessage"] = "✨ Company created successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "Company", "0", ex.Message);

                TempData["ErrorMessage"] = "❌ Failed to create company!";
                return View(model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            var company = await _unitOfWork.Companies.GetByIdAsync(id);

            if (company == null || company.IsDeleted)
            {
                TempData["ErrorMessage"] = "Company not found!";
                return RedirectToAction("Index");
            }

            return View(company);
        }

        // ✅ UPDATE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Company model)
        {
            try
            {
                var existing = await _unitOfWork.Companies.GetByIdAsync(model.Id);

                if (existing == null)
                    return NotFound();

                // 🔥 UPDATE FIELDS
                existing.Name = model.Name;
                existing.Mobile = model.Mobile;
                existing.Email = model.Email;
                existing.Cocode = model.Cocode;
                existing.IsActive = model.IsActive;

                // 🔥 AUDIT UPDATE
                existing.ModifiedOn = DateTime.Now;
                existing.ModifiedBy = GetUser();

                _unitOfWork.Companies.Update(existing);
                await _unitOfWork.SaveAsync();

                await _audit.LogAsync(
                    "Update",
                    "Company",
                    model.Id.ToString(),
                    $"Updated Company: {model.Name}"
                );

                TempData["InfoMessage"] = "✏️ Company updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "Company", model.Id.ToString(), ex.Message);

                TempData["ErrorMessage"] = "❌ Failed to update company!";
                return View(model);
            }
        }

        // ✅ SOFT DELETE (AJAX)
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var company = await _unitOfWork.Companies.GetByIdAsync(id);

                if (company == null)
                {
                    return Json(new { success = false, message = "Company not found!" });
                }

                // 🔥 SOFT DELETE INSTEAD OF HARD DELETE
                company.IsDeleted = true;
                company.ModifiedOn = DateTime.Now;
                company.ModifiedBy = GetUser();

                _unitOfWork.Companies.Update(company);
                await _unitOfWork.SaveAsync();

                await _audit.LogAsync(
                    "Delete",
                    "Company",
                    id.ToString(),
                    $"Soft Deleted Company: {company.Name}"
                );

                return Json(new { success = true, message = "Company deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync("Error", "Company", id.ToString(), ex.Message);

                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}