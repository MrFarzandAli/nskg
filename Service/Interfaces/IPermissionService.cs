namespace Nskg.Service.Interfaces
{
    public interface IPermissionService
    {
        Task<bool> HasPermissionAsync(string userId, string controller, string action, string permissionType);
    }
}
