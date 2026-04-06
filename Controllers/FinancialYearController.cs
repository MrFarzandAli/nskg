//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Rendering;
//using Nskg.Models;
//using Nskg.Repositories.Interfaces;

//namespace Nskg.Controllers
//{
//    [Authorize(Roles = "Admin")]
//    public class FinancialYearController : Controller
//    {
//        private readonly IUnitOfWork _unitOfWork;

//        public FinancialYearController(IUnitOfWork unitOfWork)
//        {
//            _unitOfWork = unitOfWork;
//        }

//        public async Task<IActionResult> Index()
//        {
//            var years = await _unitOfWork.FinancialYearRepository.GetAllWithCompanyAsync();
//            return View(years);
//        }

//        public async Task<IActionResult> Create()
//        {
//            ViewBag.Companies = new SelectList(
//                await _unitOfWork.Companies.GetAllAsync(), "Id", "Name");

//            return View();
//        }


//        [HttpPost]
//        public async Task<IActionResult> Create(FinancialYear model)
//        {
//            model.YearName = $"{model.StartDate:yyyy}-{model.EndDate:yyyy}";
//            await _unitOfWork.FinancialYears.AddAsync(model);
//            await _unitOfWork.SaveAsync();

//            TempData["SuccessMessage"] = "✨ Financial Year created successfully!";
//            return RedirectToAction("Index");
//        }



//        public async Task<IActionResult> Edit(int id)
//        {


//            var data = await _unitOfWork.FinancialYears.GetByIdAsync(id);
//            ViewBag.Companies = new SelectList(
//            await _unitOfWork.Companies.GetAllAsync(), "Id", "Name", data.CompanyId);
//            if (data == null)
//            {
//                TempData["ErrorMessage"] = "FinancialYears not found!";
//                return RedirectToAction("Index");
//            }
//            return View(data);
//        }


//        [HttpPost]
//        public async Task<IActionResult> Edit(FinancialYear model)
//        {
//            model.YearName = $"{model.StartDate:yyyy}-{model.EndDate:yyyy}";
//            _unitOfWork.FinancialYears.Update(model);
//            await _unitOfWork.SaveAsync();

//            TempData["InfoMessage"] = "✏️ Financial Year updated successfully!";
//            return RedirectToAction("Index");
//        }


//        // DELETE Action for SweetAlert (AJAX based)
//        [HttpDelete]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Delete(int id)
//        {
//            try
//            {
//                var data = await _unitOfWork.FinancialYears.GetByIdAsync(id);
//                if (data == null)
//                {
//                    return Json(new { success = false, message = "FinancialYears not found!" });
//                }

//                _unitOfWork.FinancialYears.Delete(data);
//                await _unitOfWork.SaveAsync();

//                return Json(new { success = true, message = "FinancialYears deleted successfully!" });
//            }
//            catch (Exception ex)
//            {
//                return Json(new { success = false, message = ex.Message });
//            }
//        }
//    }
//}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nskg.Models;
using Nskg.Repositories.Interfaces;

namespace Nskg.Controllers
{
    [Authorize(Roles = "Admin")]
    public class FinancialYearController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditService _audit;   // ✅ ADD

        public FinancialYearController(IUnitOfWork unitOfWork, IAuditService audit)
        {
            _unitOfWork = unitOfWork;
            _audit = audit;   // ✅ ADD
        }

        public async Task<IActionResult> Index()
        {
            var years = await _unitOfWork.FinancialYearRepository.GetAllWithCompanyAsync();
            return View(years);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Companies = new SelectList(
                await _unitOfWork.Companies.GetAllAsync(), "Id", "Name");

            return View();
        }

        // ✅ CREATE
        [HttpPost]
        public async Task<IActionResult> Create(FinancialYear model)
        {
            try
            {
                model.YearName = $"{model.StartDate:yyyy}-{model.EndDate:yyyy}";

                await _unitOfWork.FinancialYears.AddAsync(model);
                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
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
                // 🔥 ERROR LOG
                await _audit.LogAsync(
                    "Error",
                    "FinancialYears",
                    "0",
                    ex.Message
                );

                TempData["ErrorMessage"] = "❌ Failed to create Financial Year!";
                return View(model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            var data = await _unitOfWork.FinancialYears.GetByIdAsync(id);

            if (data == null)
            {
                TempData["ErrorMessage"] = "FinancialYears not found!";
                return RedirectToAction("Index");
            }

            ViewBag.Companies = new SelectList(
                await _unitOfWork.Companies.GetAllAsync(), "Id", "Name", data.CompanyId);

            return View(data);
        }

        // ✅ UPDATE
        [HttpPost]
        public async Task<IActionResult> Edit(FinancialYear model)
        {
            try
            {
                model.YearName = $"{model.StartDate:yyyy}-{model.EndDate:yyyy}";

                _unitOfWork.FinancialYears.Update(model);
                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
                await _audit.LogAsync(
                    "Update",
                    "FinancialYears",
                    model.Id.ToString(),
                    $"Updated Financial Year: {model.YearName}"
                );

                TempData["InfoMessage"] = "✏️ Financial Year updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // 🔥 ERROR LOG
                await _audit.LogAsync(
                    "Error",
                    "FinancialYears",
                    model.Id.ToString(),
                    ex.Message
                );

                TempData["ErrorMessage"] = "❌ Failed to update Financial Year!";
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
                var data = await _unitOfWork.FinancialYears.GetByIdAsync(id);

                if (data == null)
                {
                    return Json(new { success = false, message = "FinancialYears not found!" });
                }

                _unitOfWork.FinancialYears.Delete(data);
                await _unitOfWork.SaveAsync();

                // 🔥 AUDIT LOG
                await _audit.LogAsync(
                    "Delete",
                    "FinancialYears",
                    id.ToString(),
                    $"Deleted Financial Year: {data.YearName}"
                );

                return Json(new { success = true, message = "FinancialYears deleted successfully!" });
            }
            catch (Exception ex)
            {
                // 🔥 ERROR LOG
                await _audit.LogAsync(
                    "Error",
                    "FinancialYears",
                    id.ToString(),
                    ex.Message
                );

                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}