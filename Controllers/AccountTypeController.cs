using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nskg.Extensions;
using Nskg.Models;
using Nskg.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

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

                model.COCODE = User.GetCompanyId().ToString();
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

        //// ✅ EDIT (POST)
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Edit(Actype model)
        //{
        //    try
        //    {
        //        if (!ModelState.IsValid)
        //            return View(model);

        //        _unitOfWork.Actype.Update(model);
        //        await _unitOfWork.SaveAsync();

        //        // 🔥 AUDIT LOG
        //        await _audit.LogAsync(
        //            "Update",
        //            "AccountType",
        //            model.COCODE.ToString(),
        //            $"Updated Account Type: {model.ACNAME}"
        //        );

        //        TempData["InfoMessage"] = "✏️ Account Type updated successfully!";
        //        return RedirectToAction("Index");
        //    }
        //    catch (Exception ex)
        //    {
        //        await _audit.LogAsync(
        //            "Error",
        //            "AccountType",
        //            model.COCODE.ToString(),
        //            ex.Message
        //        );

        //        TempData["ErrorMessage"] = "❌ Failed to update Account Type!";
        //        return View(model);
        //    }
        //}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Actype model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(model);

                // ✅ FIX
                model.COCODE = User.GetCompanyId().ToString();

                _unitOfWork.Actype.Update(model);
                await _unitOfWork.SaveAsync();

                await _audit.LogAsync(
                    "Update",
                    "AccountType",
                    model.COCODE,
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
                    model.COCODE ?? "0",
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
                // Log the error
                await _audit.LogAsync(
                    "Error",
                    "AccountType",
                    id,
                    ex.Message
                );

                // If this is a database update error due to foreign key constraint (related data exists),
                // return a friendly message instead of the raw exception text.
                var baseEx = ex.GetBaseException();
                if (ex is DbUpdateException || (baseEx != null &&
                    (baseEx.Message.Contains("REFERENCE", System.StringComparison.OrdinalIgnoreCase) ||
                     baseEx.Message.Contains("foreign key", System.StringComparison.OrdinalIgnoreCase) ||
                     baseEx.Message.Contains("constraint", System.StringComparison.OrdinalIgnoreCase))))
                {
                    return Json(new { success = false, message = "Cannot delete Account Type because related records exist. Remove related records first." });
                }

                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
