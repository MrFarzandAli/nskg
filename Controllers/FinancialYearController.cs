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

        public FinancialYearController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
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

        [HttpPost]
        public async Task<IActionResult> Create(FinancialYear model)
        {
            model.YearName = $"{model.StartDate:yyyy}-{model.EndDate:yyyy}";

            await _unitOfWork.FinancialYears.AddAsync(model);
            await _unitOfWork.SaveAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(int id)
        {
            var data = await _unitOfWork.FinancialYears.GetByIdAsync(id);

            ViewBag.Companies = new SelectList(
                await _unitOfWork.Companies.GetAllAsync(), "Id", "Name", data.CompanyId);

            return View(data);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(FinancialYear model)
        {
            model.YearName = $"{model.StartDate:yyyy}-{model.EndDate:yyyy}";

            _unitOfWork.FinancialYears.Update(model);
            await _unitOfWork.SaveAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Delete(int id)
        {
            var data = await _unitOfWork.FinancialYears.GetByIdAsync(id);

            _unitOfWork.FinancialYears.Delete(data);
            await _unitOfWork.SaveAsync();

            return RedirectToAction("Index");
        }
    }
}
