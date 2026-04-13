
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nskg.Data;
using Nskg.Models.ViewModels;
using Nskg.Repositories.Interfaces; // ✅ for IAuditService
using System.Security.Claims;

namespace Nskg.Controllers
{
    [Authorize]
    public class AccountSetupController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IAuditService _audit; // ✅ ADD

        public AccountSetupController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IAuditService audit) // ✅ ADD
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _audit = audit; // ✅ ADD
        }

        public IActionResult SelectCompany()
        {
            var companies = _context.Companies
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                }).ToList();

            var model = new CompanySelectionViewModel
            {
                Companies = companies
            };

            // 🔥 DEFAULT COMPANY SELECT
            if (companies.Any())
            {
                model.SelectedCompanyId = int.Parse(companies.First().Value);

                // 🔥 US COMPANY KE YEARS LOAD KARO
                var years = _context.FinancialYears
                    .Where(x => x.CompanyId == model.SelectedCompanyId && !x.IsClosed)
                    .Select(y => new SelectListItem
                    {
                        Value = y.Id.ToString(),
                        Text = y.YearName
                    }).ToList();

                model.FinancialYears = years;

                // 🔥 DEFAULT YEAR SELECT
                if (years.Any())
                {
                    model.SelectedFinancialYearId = int.Parse(years.First().Value);
                }
            }

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
            try
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

                // 🔹 Refresh SignIn (VERY IMPORTANT)
                await _signInManager.RefreshSignInAsync(user);

                // 🔥 AUDIT LOG
                await _audit.LogAsync(
                    "Select",
                    "AccountSetup",
                    user.Id,
                    $"Selected Company: {model.SelectedCompanyId}, Financial Year: {model.SelectedFinancialYearId}"
                );

                TempData["SuccessMessage"] = "✅ Company and Financial Year selected successfully!";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(
                    "Error",
                    "AccountSetup",
                    "0",
                    ex.Message
                );

                TempData["ErrorMessage"] = "❌ Failed to select Company or Financial Year!";
                return View(model);
            }
        }
    }
}