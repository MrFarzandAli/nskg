using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nskg.Models.ViewModels;

namespace Nskg.Controllers
{

    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // LIST USERS
        public IActionResult Index()
        {
            var users = _userManager.Users.ToList();
            return View(users);
        }

        // ASSIGN ROLE (GET)
        public IActionResult AssignRole(string userId)
        {
            var user = _userManager.FindByIdAsync(userId).Result;

            var model = new AssignRoleViewModel
            {
                UserId = user.Id,
                Email = user.Email,
                Roles = _roleManager.Roles
                    .Select(r => new SelectListItem
                    {
                        Value = r.Name,
                        Text = r.Name
                    }).ToList()
            };

            return View(model);
        }

        // ASSIGN ROLE (POST)
        [HttpPost]
        public async Task<IActionResult> AssignRole(AssignRoleViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            await _userManager.AddToRoleAsync(user, model.SelectedRole);

            return RedirectToAction("Index");
        }
    }
}
