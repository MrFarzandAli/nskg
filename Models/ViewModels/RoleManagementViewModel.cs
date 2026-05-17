using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nskg.Models.Security;

namespace Nskg.Models.ViewModels
{
    public class RoleManagementViewModel
    {
        // For Add Role
        public string NewRoleName { get; set; }


        // For Add Form
        public string NewFormName { get; set; }
        public string NewController { get; set; }
        public string NewAction { get; set; }
        // For Assign Forms to Role
        public string SelectedRoleId { get; set; }
        public List<FormPermissionCheckbox> FormsWithPermissions { get; set; } = new List<FormPermissionCheckbox>();

        // For Assign Role to User
        public string SelectedUserId { get; set; }
        public List<string> SelectedRoleNames { get; set; } = new List<string>();

        // Data Lists - Using Security namespace classes
        public List<IdentityRole> Roles { get; set; }
        public List<Nskg.Models.Security.Form> Forms { get; set; }           // ← FIXED: Full namespace
        public List<IdentityUser> Users { get; set; }
        public List<Nskg.Models.Security.RoleFormPermission> Permissions { get; set; }  // ← FIXED: Full namespace

        // For Dropdowns
        public List<SelectListItem> RoleList { get; set; }
        public List<SelectListItem> FormList { get; set; }

        // For Display - Using Security namespace
        public Dictionary<string, List<string>> UserRolesMap { get; set; } = new();
        public Dictionary<string, List<Nskg.Models.Security.RoleFormPermission>> RoleFormsMap { get; set; } = new();  // ← FIXED: Full namespace
    }

    public class FormPermissionCheckbox
    {
        public int FormId { get; set; }
        public string FormName { get; set; }
        public string Controller { get; set; }
        public string Action { get; set; }
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}