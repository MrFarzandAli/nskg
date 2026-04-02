using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nskg.Data;
using Nskg.Models.ViewModels;
using System.Security.Claims;

namespace Nskg.Controllers
{ 
    [Authorize]
    public class AccountSetupController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public AccountSetupController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // 🔹 SHOW SCREEN
        public IActionResult SelectCompany()
        {
            var model = new CompanySelectionViewModel
            {
                Companies = _context.Companies
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.Name
                    }).ToList(),

                FinancialYears = new List<SelectListItem>() // initially empty
            };

            return View(model);
        }

        // 🔹 LOAD FINANCIAL YEARS (AJAX optional)
        public IActionResult GetFinancialYears(int companyId)
        {
            var years = _context.FinancialYears
                .Where(x => x.CompanyId == companyId && !x.IsClosed)
                .Select(y => new SelectListItem
                {
                    Value = y.Id.ToString(),
                    Text = y.YearName
                }).ToList();

            return Json(years);
        }

        // 🔹 SAVE SELECTION
        [HttpPost]
        public async Task<IActionResult> SelectCompany(CompanySelectionViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            // OLD claims remove
            var existingClaims = await _userManager.GetClaimsAsync(user);

            var companyClaim = existingClaims.FirstOrDefault(c => c.Type == "CompanyId");
            var yearClaim = existingClaims.FirstOrDefault(c => c.Type == "FinancialYearId");

            if (companyClaim != null)
                await _userManager.RemoveClaimAsync(user, companyClaim);

            if (yearClaim != null)
                await _userManager.RemoveClaimAsync(user, yearClaim);

            // ADD NEW CLAIMS
            await _userManager.AddClaimAsync(user, new Claim("CompanyId", model.SelectedCompanyId.ToString()));
            await _userManager.AddClaimAsync(user, new Claim("FinancialYearId", model.SelectedFinancialYearId.ToString()));

            // 🔥 IMPORTANT: Refresh SignIn (VERY IMPORTANT)
            await _signInManager.RefreshSignInAsync(user);

            return RedirectToAction("Index", "Home");
        }
    }
}
