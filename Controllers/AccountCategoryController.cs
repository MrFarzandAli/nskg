using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nskg.Extensions;
using Nskg.Models;
using Nskg.Models.ViewModels;
using Nskg.Repositories.Interfaces;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

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
        private string GetUser()
        {
            return User?.Identity?.Name ?? "System";
        }
        // ✅ INDEX
        public async Task<IActionResult> Index()
        {
            int companyId = User.GetCompanyId();
            var accCats = (await _unitOfWork.AccCat.GetAllAsync())
                                       .Where(x => !x.IsDeleted && (x.CompanyId == companyId || x.CoCode == companyId.ToString())); 

            return View(accCats);
        }
       

        // ✅ CREATE (GET)
        public IActionResult Create()
        {
            return View();
        }

        // ✅ CREATE (POST)
        [HttpPost]
        public async Task<IActionResult> Create(AccCatViewModel vm)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(vm);

                int companyId = User.GetCompanyId();
                string companycode = User.GetCompanyCode();


                var model = new AccCat
                {
                    CatCode = vm.CatCode,
                    Category = vm.Category,
                    CoCode = companycode,
                    CompanyId = companyId
                };
                model.CreatedOn = DateTime.Now;
                model.CreatedBy = GetUser();
                model.IsDeleted = false;
                await _unitOfWork.AccCat.AddAsync(model);
                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
                await _audit.LogAsync(
                    "Create",
                    "AccountCategory",
                    model.Id.ToString(),
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
                return View(vm);
            }
        }



        // ✅ EDIT (GET)
        public async Task<IActionResult> Edit(int id)
        {
            var accCat = await _unitOfWork.AccCat.GetByIdAsync(id);

            if (accCat == null)
            {
                TempData["ErrorMessage"] = "Account Category not found!";
                return RedirectToAction("Index");
            }

            return View(accCat);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AccCat model)
        {
            try
            {
               

                // ✅ Step 1: existing record fetch karo
                var existing = await _unitOfWork.AccCat.GetByIdAsync(model.Id);

                if (existing == null)
                {
                    TempData["ErrorMessage"] = "Record not found!";
                    return RedirectToAction("Index");
                }

                // ✅ Step 2: fields update karo
                existing.CatCode = model.CatCode;
                existing.Category = model.Category;
                existing.CompanyId = User.GetCompanyId();
                existing.CoCode = User.GetCompanyCode();

                existing.ModifiedOn = DateTime.Now;
                existing.ModifiedBy = GetUser();

                // ✅ Step 3: update
                _unitOfWork.AccCat.Update(existing);
                await _unitOfWork.SaveAsync();

                await _audit.LogAsync(
                    "Update",
                    "AccountCategory",
                    existing.Id.ToString(),
                    $"Updated Account Category: {existing.Category}"
                );

                TempData["InfoMessage"] = "✏️ Account Category updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(
                    "Error",
                    "AccountCategory",
                    model.Id.ToString(),
                    ex.Message
                );

                TempData["ErrorMessage"] = "❌ Failed to update Account Category!";
                return View(model);
            }
        }

        //// ✅ DELETE (AJAX - BEST PRACTICE)
       
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var accCat = await _unitOfWork.AccCat.GetByIdAsync(id);

                if (accCat == null)
                {
                    return Json(new { success = false, message = "Account Category not found!" });
                }

                // ✅ Soft Delete
                accCat.IsDeleted = true;
                accCat.ModifiedOn = DateTime.Now;
                accCat.ModifiedBy = GetUser();

                // ❗ IMPORTANT: Update call karo, Delete nahi
                _unitOfWork.AccCat.Update(accCat);
                await _unitOfWork.SaveAsync();

                await _audit.LogAsync(
                    "Delete",
                    "AccountCategory",
                    id.ToString(),
                    $"Soft Deleted Account Category: {accCat.Category}"
                );

                return Json(new { success = true, message = "Account Category deleted successfully!" });
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(
                    "Error",
                    "AccountCategory",
                    id.ToString(),
                    ex.Message
                );

                return Json(new { success = false, message = ex.Message });
            }
        }


    }
}
