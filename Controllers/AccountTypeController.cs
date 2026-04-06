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

        public AccountTypeController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> Index()
        {
            var actypes = await _unitOfWork.Actype.GetAllAsync();
            return View(actypes);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Actype model)
        {
            if (ModelState.IsValid)
            {
                await _unitOfWork.Actype.AddAsync(model);
                await _unitOfWork.SaveAsync();

                return RedirectToAction("Index");
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(string id)
        {
            var actype = await _unitOfWork.Actype.GetByIdAsync(id);
            return View(actype);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Actype model)
        {
            _unitOfWork.Actype.Update(model);
            await _unitOfWork.SaveAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Delete(string id)
        {
            var actype = await _unitOfWork.Actype.GetByIdAsync(id);

            _unitOfWork.Actype.Delete(actype);
            await _unitOfWork.SaveAsync();

            return RedirectToAction("Index");
        }
    }
}

