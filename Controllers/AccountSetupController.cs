using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nskg.Data;
using Nskg.Helper;
using Nskg.Models;
using Nskg.Models.ViewModels;
using Nskg.Repositories.Interfaces; // ✅ for IAuditService
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Nskg.Controllers
{
    [Authorize]
    public class AccountSetupController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IAuditService _audit;

        public AccountSetupController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IAuditService audit)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _audit = audit;
        }

        public IActionResult SelectCompany()
        {
            var companies = _context.Companies
                .Where(c => !c.IsDeleted)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                }).ToList();

            var model = new CompanySelectionViewModel
            {
                Companies = companies
            };

            var existingCookie = CompanyCookieHelper.GetCompanyCookie(Request);
            int defaultCompanyId = 0;

            if (existingCookie != null && existingCookie.CompanyId > 0 && companies.Any(c => c.Value == existingCookie.CompanyId.ToString()))
            {
                defaultCompanyId = existingCookie.CompanyId;
            }
            else if (companies.Any())
            {
                defaultCompanyId = int.Parse(companies.First().Value);
            }

            if (defaultCompanyId > 0)
            {
                model.SelectedCompanyId = defaultCompanyId;

                var years = _context.FinancialYears
                    .Where(x => x.CompanyId == defaultCompanyId && !x.IsClosed)
                    .Select(y => new SelectListItem
                    {
                        Value = y.Id.ToString(),
                        Text = y.YearName
                    }).ToList();

                model.FinancialYears = years;

                if (existingCookie != null && existingCookie.FinancialYearId > 0 && years.Any(y => y.Value == existingCookie.FinancialYearId.ToString()))
                {
                    model.SelectedFinancialYearId = existingCookie.FinancialYearId;
                }
                else if (years.Any())
                {
                    model.SelectedFinancialYearId = int.Parse(years.First().Value);
                }
            }

            return View(model);
        }

        // 🔹 LOAD FINANCIAL YEARS (AJAX)
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

        // 🔹 SWITCH COMPANY ACTION (Clears current cookie and opens company selection)
        [HttpGet]
        public IActionResult SwitchCompany()
        {
            CompanyCookieHelper.ClearCompanyCookie(Response);
            return RedirectToAction("SelectCompany");
        }

        // 🔹 SAVE SELECTION
        [HttpPost]
        public async Task<IActionResult> SelectCompany(CompanySelectionViewModel model)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                // Clean up any old database claims so they never cause cross-machine conflicts
                if (user != null)
                {
                    var existingClaims = await _userManager.GetClaimsAsync(user);
                    var oldCompanyClaims = existingClaims
                        .Where(c => c.Type is "CompanyId" or "CompanyCode" or "CompanyName" or "FinancialYearId")
                        .ToList();

                    foreach (var c in oldCompanyClaims)
                    {
                        await _userManager.RemoveClaimAsync(user, c);
                    }
                }

                var company = _context.Companies.FirstOrDefault(x => x.Id == model.SelectedCompanyId);

                var selectedCompId = model.SelectedCompanyId ?? 0;
                var selectedYearId = model.SelectedFinancialYearId ?? 0;

                // 🔹 SAVE STRICTLY IN BROWSER COOKIE (Isolated per machine / browser session)
                var cookieContext = new CompanyCookieContext
                {
                    CompanyId = selectedCompId,
                    CompanyCode = company?.Cocode ?? "",
                    CompanyName = company?.Name ?? "",
                    FinancialYearId = selectedYearId
                };

                CompanyCookieHelper.SetCompanyCookie(Response, cookieContext, Request.IsHttps);

                // 🔥 AUDIT LOG
                if (user != null)
                {
                    await _audit.LogAsync(
                        "Select",
                        "AccountSetup",
                        user.Id,
                        $"Selected Company: {selectedCompId} ({company?.Name}), Financial Year: {selectedYearId}",
                        companyId: selectedCompId,
                        financialYearId: selectedYearId
                    );
                }

                TempData["SuccessMessage"] = $"✅ Company [{company?.Name}] selected successfully!";
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