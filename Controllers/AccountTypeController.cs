using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nskg.Extensions;
using Nskg.Models;
using Nskg.Repositories.Interfaces;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

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
        private string GetUser()
        {
            return User?.Identity?.Name ?? "System";
        }
        // ✅ INDEX
     
        public async Task<IActionResult> Index()
        {
            var actypes = (await _unitOfWork.Actype.GetAllAsync())
                          .Where(x => x.IsDeleted == false);

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
                // 🔥 SET AUDIT FIELDS
                model.CreatedOn = DateTime.Now;
                model.CreatedBy = GetUser();
                model.IsDeleted = false;
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
       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Actype model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(model);

                // 🔥 STEP 1: DB se existing record lao
                var existing = await _unitOfWork.Actype.GetByIdAsync(model.ACTYPE); // 👈 apni PK use karo

                if (existing == null)
                    return NotFound();

                // 🔥 STEP 2: sirf editable fields update karo
                existing.ACNAME = model.ACNAME;
                existing.COCODE = User.GetCompanyId().ToString();


                // 🔥 STEP 3: audit fields
                existing.ModifiedOn = DateTime.Now;
                existing.ModifiedBy = GetUser();

                // 🔥 STEP 4: update
                _unitOfWork.Actype.Update(existing);
                await _unitOfWork.SaveAsync();

                await _audit.LogAsync(
                    "Update",
                    "AccountType",
                    existing.COCODE,
                    $"Updated Account Type: {existing.ACNAME}"
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
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var actype = await _unitOfWork.Actype.GetByIdAsync(id);

                if (actype == null)
                {
                    return Json(new { success = false, message = "Account Type not found!" });
                }
                // 🔥 SOFT DELETE INSTEAD OF HARD DELETE
                actype.IsDeleted = true;
                actype.ModifiedOn = DateTime.Now;
                actype.ModifiedBy = GetUser();
                //  _unitOfWork.Actype.Delete(actype);
                _unitOfWork.Actype.Update(actype);

                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
                await _audit.LogAsync(
                    "Delete",
                    "AccountType",
                    id.ToString(),
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
                    id.ToString(),
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
