using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Models.Security;
using Nskg.Models.ViewModels;

namespace Nskg.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RoleManagementController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public RoleManagementController(
            RoleManager<IdentityRole> roleManager,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            var users = await _userManager.Users.ToListAsync();
            var forms = await _context.Set<Form>().ToListAsync();
            var permissions = await _context.Set<RoleFormPermission>().ToListAsync();

            // Get User-Roles Mapping
            var userRolesMap = new Dictionary<string, List<string>>();
            foreach (var user in users)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                userRolesMap[user.Id] = userRoles.ToList();
            }

            // Get Role-Forms Mapping
            var roleFormsMap = new Dictionary<string, List<RoleFormPermission>>();
            foreach (var role in roles)
            {
                var rolePermissions = permissions.Where(x => x.RoleId == role.Id).ToList();
                roleFormsMap[role.Id] = rolePermissions;
            }

            // Prepare Forms with Permissions (empty initially)
            var formsWithPermissions = new List<FormPermissionCheckbox>();
            foreach (var form in forms)
            {
                formsWithPermissions.Add(new FormPermissionCheckbox
                {
                    FormId = form.Id,
                    FormName = form.Name,
                    Controller = form.Controller,
                    Action = form.Action,
                    CanView = false,
                    CanCreate = false,
                    CanEdit = false,
                    CanDelete = false
                });
            }

            var model = new RoleManagementViewModel
            {
                Roles = roles,
                Users = users,
                Forms = forms,
                Permissions = permissions,
                FormsWithPermissions = formsWithPermissions,
                UserRolesMap = userRolesMap,
                RoleFormsMap = roleFormsMap
            };

            return View(model);
        }

        // POST: Add New Role
        [HttpPost]
        public async Task<IActionResult> AddRole(RoleManagementViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.NewRoleName))
            {
                var roleExists = await _roleManager.RoleExistsAsync(model.NewRoleName);
                if (!roleExists)
                {
                    var result = await _roleManager.CreateAsync(new IdentityRole(model.NewRoleName));
                    if (result.Succeeded)
                    {
                        TempData["Success"] = $"Role '{model.NewRoleName}' created successfully!";
                    }
                    else
                    {
                        TempData["Error"] = "Failed to create role.";
                    }
                }
                else
                {
                    TempData["Info"] = "Role already exists!";
                }
            }
            else
            {
                TempData["Error"] = "Role name cannot be empty!";
            }

            return RedirectToAction("Index");
        }

        // POST: Delete Role
        [HttpPost]
        public async Task<IActionResult> DeleteRole(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role != null)
            {
                // First delete all permissions for this role
                var permissions = _context.Set<RoleFormPermission>().Where(x => x.RoleId == roleId);
                _context.Set<RoleFormPermission>().RemoveRange(permissions);
                await _context.SaveChangesAsync();

                // Then delete the role
                var result = await _roleManager.DeleteAsync(role);
                if (result.Succeeded)
                {
                    TempData["Success"] = $"Role '{role.Name}' deleted successfully!";
                }
                else
                {
                    TempData["Error"] = "Failed to delete role.";
                }
            }
            return RedirectToAction("Index");
        }

        // POST: Assign Forms to Role
        [HttpPost]
        public async Task<IActionResult> AssignFormsToRole(RoleManagementViewModel model)
        {
            if (!string.IsNullOrEmpty(model.SelectedRoleId) && model.FormsWithPermissions.Any())
            {
                // Remove existing permissions
                var existingPermissions = _context.Set<RoleFormPermission>()
                    .Where(x => x.RoleId == model.SelectedRoleId)
                    .ToList();

                if (existingPermissions.Any())
                {
                    _context.Set<RoleFormPermission>().RemoveRange(existingPermissions);
                }

                // Add new permissions
                foreach (var perm in model.FormsWithPermissions)
                {
                    var roleFormPermission = new RoleFormPermission
                    {
                        RoleId = model.SelectedRoleId,
                        FormId = perm.FormId,
                        CanView = perm.CanView,
                        CanCreate = perm.CanCreate,
                        CanEdit = perm.CanEdit,
                        CanDelete = perm.CanDelete
                    };
                    await _context.Set<RoleFormPermission>().AddAsync(roleFormPermission);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Forms and permissions assigned to role successfully!";
            }
            else
            {
                TempData["Error"] = "Please select a role and configure form permissions.";
            }

            return RedirectToAction("Index");
        }

        // POST: Assign Role to User
        [HttpPost]
        public async Task<IActionResult> AssignRoleToUser(RoleManagementViewModel model)
        {
            if (!string.IsNullOrEmpty(model.SelectedUserId) && model.SelectedRoleNames != null && model.SelectedRoleNames.Any())
            {
                var user = await _userManager.FindByIdAsync(model.SelectedUserId);
                if (user != null)
                {
                    // Remove existing roles
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);

                    // Add new roles
                    var result = await _userManager.AddToRolesAsync(user, model.SelectedRoleNames);
                    if (result.Succeeded)
                    {
                        TempData["Success"] = $"Roles assigned to user '{user.UserName}' successfully!";
                    }
                    else
                    {
                        TempData["Error"] = "Failed to assign roles to user.";
                    }
                }
                else
                {
                    TempData["Error"] = "User not found!";
                }
            }
            else
            {
                TempData["Error"] = "Please select both user and at least one role.";
            }

            return RedirectToAction("Index");
        }


        // POST: Add New Form
        [HttpPost]
        public async Task<IActionResult> AddForm(RoleManagementViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.NewFormName) &&
                !string.IsNullOrWhiteSpace(model.NewController) &&
                !string.IsNullOrWhiteSpace(model.NewAction))
            {
                // Check if form already exists
                var formExists = await _context.Set<Form>()
                    .AnyAsync(f => f.Name == model.NewFormName);

                if (!formExists)
                {
                    var newForm = new Form
                    {
                        Name = model.NewFormName,
                        Controller = model.NewController,
                        Action = model.NewAction
                    };

                    await _context.Set<Form>().AddAsync(newForm);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Form '{model.NewFormName}' created successfully!";
                }
                else
                {
                    TempData["Info"] = "Form already exists!";
                }
            }
            else
            {
                TempData["Error"] = "Please fill all fields (Form Name, Controller, Action)!";
            }

            return RedirectToAction("Index");
        }

        // POST: Delete Form
        [HttpPost]
        public async Task<IActionResult> DeleteForm(int formId)
        {
            var form = await _context.Set<Form>().FindAsync(formId);
            if (form != null)
            {
                // First delete all permissions associated with this form
                var permissions = _context.Set<RoleFormPermission>().Where(x => x.FormId == formId);
                _context.Set<RoleFormPermission>().RemoveRange(permissions);

                // Then delete the form
                _context.Set<Form>().Remove(form);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Form '{form.Name}' deleted successfully!";
            }
            else
            {
                TempData["Error"] = "Form not found!";
            }

            return RedirectToAction("Index");
        }

        // GET: Get Role Permissions (for AJAX)
        [HttpGet]
        public async Task<IActionResult> GetRolePermissions(string roleId)
        {
            var permissions = await _context.Set<RoleFormPermission>()
                .Where(x => x.RoleId == roleId)
                .ToListAsync();

            var allForms = await _context.Set<Form>().ToListAsync();

            var formsWithPermissions = new List<FormPermissionCheckbox>();
            foreach (var form in allForms)
            {
                var existingPerm = permissions.FirstOrDefault(x => x.FormId == form.Id);
                formsWithPermissions.Add(new FormPermissionCheckbox
                {
                    FormId = form.Id,
                    FormName = form.Name,
                    Controller = form.Controller,
                    Action = form.Action,
                    CanView = existingPerm?.CanView ?? false,
                    CanCreate = existingPerm?.CanCreate ?? false,
                    CanEdit = existingPerm?.CanEdit ?? false,
                    CanDelete = existingPerm?.CanDelete ?? false
                });
            }

            return Json(formsWithPermissions);
        }
    }
}