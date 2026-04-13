using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nskg.Models;
using Nskg.Repositories.Interfaces;

namespace Nskg.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AccountCategoryController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditService _audit; // ✅ ADD

        public AccountCategoryController(IUnitOfWork unitOfWork, IAuditService audit)
        {
            _unitOfWork = unitOfWork;
            _audit = audit; // ✅ ADD
        }

        // ✅ INDEX
        public async Task<IActionResult> Index()
        {
            var accCats = await _unitOfWork.AccCat.GetAllAsync();
            return View(accCats);
        }

        // ✅ CREATE (GET)
        public IActionResult Create()
        {
            return View();
        }

        // ✅ CREATE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AccCat model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(model);

                await _unitOfWork.AccCat.AddAsync(model);
                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
                await _audit.LogAsync(
                    "Create",
                    "AccountCategory",
                    model.CatCode.ToString(),
                    $"Created Account Category: {model.Category}"
                   
                );

                TempData["SuccessMessage"] = "✨ Account Category created successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(
                    "Error",
                    "AccountCategory",
                    "0",
                    ex.Message
                );

                TempData["ErrorMessage"] = "❌ Failed to create Account Category!";
                return View(model);
            }
        }

        // ✅ EDIT (GET)
        public async Task<IActionResult> Edit(string id)
        {
            var accCat = await _unitOfWork.AccCat.GetByIdAsync(id);

            if (accCat == null)
            {
                TempData["ErrorMessage"] = "Account Category not found!";
                return RedirectToAction("Index");
            }

            return View(accCat);
        }

        // ✅ EDIT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AccCat model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(model);

                _unitOfWork.AccCat.Update(model);
                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
                await _audit.LogAsync(
                    "Update",
                    "AccountCategory",
                    model.CatCode
                    .ToString(),
                    $"Updated Account Category: {model.Category}"
                );

                TempData["InfoMessage"] = "✏️ Account Category updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(
                    "Error",
                    "AccountCategory",
                    model.CatCode.ToString(),
                    ex.Message
                );

                TempData["ErrorMessage"] = "❌ Failed to update Account Category!";
                return View(model);
            }
        }

        // ✅ DELETE (AJAX - BEST PRACTICE)
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var accCat = await _unitOfWork.AccCat.GetByIdAsync(id);

                if (accCat == null)
                {
                    return Json(new { success = false, message = "Account Category not found!" });
                }

                _unitOfWork.AccCat.Delete(accCat);
                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
                await _audit.LogAsync(
                    "Delete",
                    "AccountCategory",
                    id,
                    $"Deleted Account Category: {accCat.Category}"
                );

                return Json(new { success = true, message = "Account Category deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(
                    "Error",
                    "AccountCategory",
                    id,
                    ex.Message
                );

                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
