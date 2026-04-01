using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Models.Security;
using Nskg.Models.ViewModels;

namespace Nskg.Controllers
{   

    [Authorize(Roles = "Admin")]
    public class SecurityController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public SecurityController(
            RoleManager<IdentityRole> roleManager,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _context = context;
        }

        public IActionResult Index()
        {
            var model = new SecurityViewModel
            {
                Roles = _roleManager.Roles.ToList(),
                Forms = _context.Set<Form>().ToList(),
                Users = _userManager.Users.ToList(),
                Permissions = _context.Set<RoleFormPermission>().ToList(),

                RoleList = _roleManager.Roles
                    .Select(r => new SelectListItem { Value = r.Id, Text = r.Name }).ToList(),

                FormList = _context.Set<Form>()
                    .Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList()
            };

            return View(model);
        }

        // 🔹 CREATE ROLE
        [HttpPost]
        public async Task<IActionResult> CreateRole(SecurityViewModel model)
        {
            if (!string.IsNullOrEmpty(model.NewRoleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(model.NewRoleName));
            }
            return RedirectToAction("Index");
        }

        // 🔹 DELETE ROLE
        public async Task<IActionResult> DeleteRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role != null)
                await _roleManager.DeleteAsync(role);

            return RedirectToAction("Index");
        }

        // 🔹 CREATE FORM
        [HttpPost]
        public IActionResult CreateForm(SecurityViewModel model)
        {
            if (model.NewForm != null)
            {
                _context.Add(model.NewForm);
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        // 🔹 DELETE FORM
        public IActionResult DeleteForm(int id)
        {
            var form = _context.Set<Form>().Find(id);
            if (form != null)
            {
                _context.Remove(form);
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        // 🔹 ASSIGN ROLE TO USER
        [HttpPost]
        public async Task<IActionResult> AssignRoleToUser(SecurityViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.SelectedUserId);
            var role = await _roleManager.FindByIdAsync(model.SelectedRoleId);

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

            await _userManager.AddToRoleAsync(user, role.Name);

            return RedirectToAction("Index");
        }

        // 🔹 ASSIGN FORM PERMISSIONS
        [HttpPost]
        public IActionResult AssignPermission(string roleId, int formId,
            bool canView, bool canCreate, bool canEdit, bool canDelete)
        {
            var existing = _context.Set<RoleFormPermission>()
                .FirstOrDefault(x => x.RoleId == roleId && x.FormId == formId);

            if (existing == null)
            {
                _context.Add(new RoleFormPermission
                {
                    RoleId = roleId,
                    FormId = formId,
                    CanView = canView,
                    CanCreate = canCreate,
                    CanEdit = canEdit,
                    CanDelete = canDelete
                });
            }
            else
            {
                existing.CanView = canView;
                existing.CanCreate = canCreate;
                existing.CanEdit = canEdit;
                existing.CanDelete = canDelete;
            }

            _context.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}
