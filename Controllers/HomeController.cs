using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nskg.Helper;
using Nskg.Models;
using Nskg.Service.Interfaces;
using System.Diagnostics;
using System.Security.Claims;

namespace Nskg.Controllers
{
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        private readonly IPermissionService _permissionService;

        public HomeController(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }
        public IActionResult Index()
        {
            return View();
        }
       
        public async Task<IActionResult> Privacy()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var hasAccess = await _permissionService
                .HasPermissionAsync(userId, "Home", "Privacy", Permissions.View);

            if (!hasAccess)
                return RedirectToAction("AccessDenied");

            return View();
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
