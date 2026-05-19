using Nskg.Models.Security;
using Nskg.Models.ViewModels;

namespace Nskg.Services.Interfaces
{
    public interface IRoleFormPermissionService
    {
        Task<List<Form>> GetAllFormsAsync();
        Task<List<RoleFormPermission>> GetPermissionsByRoleIdAsync(string roleId);
        Task<bool> AssignFormsToRoleAsync(string roleId, List<FormPermissionCheckbox> permissions);
    }
}