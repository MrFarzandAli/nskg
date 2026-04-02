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

        public CompanyController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
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

        [HttpPost]
        public async Task<IActionResult> Create(Company model)
        {
            if (ModelState.IsValid)
            {
                await _unitOfWork.Companies.AddAsync(model);
                await _unitOfWork.SaveAsync();

                return RedirectToAction("Index");
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var company = await _unitOfWork.Companies.GetByIdAsync(id);
            return View(company);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Company model)
        {
            _unitOfWork.Companies.Update(model);
            await _unitOfWork.SaveAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Delete(int id)
        {
            var company = await _unitOfWork.Companies.GetByIdAsync(id);

            _unitOfWork.Companies.Delete(company);
            await _unitOfWork.SaveAsync();

            return RedirectToAction("Index");
        }
    }
}
