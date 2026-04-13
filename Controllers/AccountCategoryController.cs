using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nskg.Models;
using Nskg.Models.ViewModels;
using Nskg.Repositories.Interfaces;
using Nskg.Extensions;

namespace Nskg.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AccountCategoryController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public AccountCategoryController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> Index()
        {
            var accCats = await _unitOfWork.AccCat.GetAllAsync();
            return View(accCats);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(AccCatViewModel vm)
        {
            if (ModelState.IsValid)
            {
                int companyId = User.GetCompanyId();

                var model = new AccCat
                {
                    CatCode = vm.CatCode,
                    Category = vm.Category,
                    CoCode = companyId.ToString()
                };

                await _unitOfWork.AccCat.AddAsync(model);
                await _unitOfWork.SaveAsync();

                return RedirectToAction("Index");
            }

            return View(vm);
        }

        public async Task<IActionResult> Edit(string id)
        {
            var accCat = await _unitOfWork.AccCat.GetByIdAsync(id);
            return View(accCat);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(AccCat model)
        {
            _unitOfWork.AccCat.Update(model);
            await _unitOfWork.SaveAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Delete(string id)
        {
            var accCat = await _unitOfWork.AccCat.GetByIdAsync(id);

            _unitOfWork.AccCat.Delete(accCat);
            await _unitOfWork.SaveAsync();

            return RedirectToAction("Index");
        }
    }
}
