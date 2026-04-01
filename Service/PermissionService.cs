using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Models.Security;
using Nskg.Service.Interfaces;

namespace Nskg.Service
{    

    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public PermissionService(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<bool> HasPermissionAsync(string userId, string controller, string action, string permissionType)
        {
            // Get user roles
            var user = await _userManager.FindByIdAsync(userId);
            var roles = await _userManager.GetRolesAsync(user);

            if (!roles.Any())
                return false;

            // Get role IDs
            var roleIds = _context.Roles
                .Where(r => roles.Contains(r.Name))
                .Select(r => r.Id)
                .ToList();

            // Get form
            var form = await _context.Set<Form>()
                .FirstOrDefaultAsync(f =>
                    f.Controller == controller &&
                    f.Name == action);

            if (form == null)
                return false;

            // Get permission
            var permission = await _context.Set<RoleFormPermission>()
                .Where(p => roleIds.Contains(p.RoleId) && p.FormId == form.Id)
                .FirstOrDefaultAsync();

            if (permission == null)
                return false;

            return permissionType switch
            {
                "View" => permission.CanView,
                "Create" => permission.CanCreate,
                "Edit" => permission.CanEdit,
                "Delete" => permission.CanDelete,
                _ => false
            };
        }
    }
}
