using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Models.Security;           // ← CHANGE: Security namespace
using Nskg.Models.ViewModels;          // ← ADD: For FormPermissionCheckbox
using Nskg.Services.Interfaces;

namespace Nskg.Services
{
    public class RoleFormPermissionService : IRoleFormPermissionService
    {
        private readonly ApplicationDbContext _context;

        public RoleFormPermissionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Form>> GetAllFormsAsync()
        {
            return await _context.Set<Form>().ToListAsync();  // ← CHANGE: Use Set<Form>()
        }

        public async Task<List<RoleFormPermission>> GetPermissionsByRoleIdAsync(string roleId)
        {
            return await _context.Set<RoleFormPermission>()
                .Where(x => x.RoleId == roleId)
                .Include(x => x.Form)
                .ToListAsync();
        }

        public async Task<bool> AssignFormsToRoleAsync(string roleId, List<FormPermissionCheckbox> permissions)
        {
            try
            {
                // Remove existing permissions for this role
                var existingPermissions = await _context.Set<RoleFormPermission>()
                    .Where(x => x.RoleId == roleId)
                    .ToListAsync();

                if (existingPermissions.Any())
                {
                    _context.Set<RoleFormPermission>().RemoveRange(existingPermissions);
                }

                // Add new permissions
                foreach (var perm in permissions)
                {
                    var roleFormPermission = new RoleFormPermission
                    {
                        RoleId = roleId,
                        FormId = perm.FormId,
                        CanView = perm.CanView,
                        CanCreate = perm.CanCreate,
                        CanEdit = perm.CanEdit,
                        CanDelete = perm.CanDelete
                    };
                    await _context.Set<RoleFormPermission>().AddAsync(roleFormPermission);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
    }
}