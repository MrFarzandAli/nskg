using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nskg.Models;
using Nskg.Repositories.Interfaces;

namespace Nskg.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AccountTypeController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditService _audit; // ✅ ADD

        public AccountTypeController(IUnitOfWork unitOfWork, IAuditService audit)
        {
            _unitOfWork = unitOfWork;
            _audit = audit; // ✅ ADD
        }

        // ✅ INDEX
        public async Task<IActionResult> Index()
        {
            var actypes = await _unitOfWork.Actype.GetAllAsync();
            return View(actypes);
        }

        // ✅ CREATE (GET)
        public IActionResult Create()
        {
            return View();
        }

        // ✅ CREATE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Actype model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(model);

                await _unitOfWork.Actype.AddAsync(model);
                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
                await _audit.LogAsync(
                    "Create",
                    "AccountType",
                    model.COCODE.ToString(),
                    $"Created Account Type: {model.ACNAME}"
                );

                TempData["SuccessMessage"] = "✨ Account Type created successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(
                    "Error",
                    "AccountType",
                    "0",
                    ex.Message
                );

                TempData["ErrorMessage"] = "❌ Failed to create Account Type!";
                return View(model);
            }
        }

        // ✅ EDIT (GET)
        public async Task<IActionResult> Edit(string id)
        {
            var actype = await _unitOfWork.Actype.GetByIdAsync(id);

            if (actype == null)
            {
                TempData["ErrorMessage"] = "Account Type not found!";
                return RedirectToAction("Index");
            }

            return View(actype);
        }

        // ✅ EDIT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Actype model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(model);

                _unitOfWork.Actype.Update(model);
                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
                await _audit.LogAsync(
                    "Update",
                    "AccountType",
                    model.COCODE.ToString(),
                    $"Updated Account Type: {model.ACNAME}"
                );

                TempData["InfoMessage"] = "✏️ Account Type updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(
                    "Error",
                    "AccountType",
                    model.COCODE.ToString(),
                    ex.Message
                );

                TempData["ErrorMessage"] = "❌ Failed to update Account Type!";
                return View(model);
            }
        }

        // ✅ DELETE (AJAX)
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var actype = await _unitOfWork.Actype.GetByIdAsync(id);

                if (actype == null)
                {
                    return Json(new { success = false, message = "Account Type not found!" });
                }

                _unitOfWork.Actype.Delete(actype);
                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
                await _audit.LogAsync(
                    "Delete",
                    "AccountType",
                    id,
                    $"Deleted Account Type: {actype.ACNAME}"
                );

                return Json(new { success = true, message = "Account Type deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(
                    "Error",
                    "AccountType",
                    id,
                    ex.Message
                );

                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
