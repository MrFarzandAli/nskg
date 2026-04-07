using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nskg.Models;
using Nskg.Repositories.Interfaces;

namespace Nskg.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CompanyController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditService _audit;   // ✅ ADD

        public CompanyController(IUnitOfWork unitOfWork, IAuditService audit)
        {
            _unitOfWork = unitOfWork;
            _audit = audit;   // ✅ ADD
        }

        public async Task<IActionResult> Index()
        {
            var companies = await _unitOfWork.Companies.GetAllAsync();
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
                await _unitOfWork.Companies.AddAsync(model);
                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
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
                // 🔥 ERROR LOG
                await _audit.LogAsync(
                    "Error",
                    "Company",
                    "0",
                    ex.Message
                );

                TempData["ErrorMessage"] = "❌ Failed to create company!";
                return View(model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            var company = await _unitOfWork.Companies.GetByIdAsync(id);

            if (company == null)
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
                _unitOfWork.Companies.Update(model);
                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
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
                // 🔥 ERROR LOG
                await _audit.LogAsync(
                    "Error",
                    "Company",
                    model.Id.ToString(),
                    ex.Message
                );

                TempData["ErrorMessage"] = "❌ Failed to update company!";
                return View(model);
            }
        }

        // ✅ DELETE (AJAX)
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

                _unitOfWork.Companies.Delete(company);
                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
                await _audit.LogAsync(
                    "Delete",
                    "Company",
                    id.ToString(),
                    $"Deleted Company: {company.Name}"
                );

                return Json(new { success = true, message = "Company deleted successfully!" });
            }
            catch (Exception ex)
            {
                // 🔥 ERROR LOG
                await _audit.LogAsync(
                    "Error",
                    "Company",
                    id.ToString(),
                    ex.Message
                );

                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}