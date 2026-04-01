using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nskg.Models.Security;

namespace Nskg.Models.ViewModels
{
    public class SecurityViewModel
    {
        // Roles
        public List<IdentityRole> Roles { get; set; }
        public string NewRoleName { get; set; }

        // Forms
        public List<Form> Forms { get; set; }
        public Form NewForm { get; set; }

        // Users
        public List<IdentityUser> Users { get; set; }
        public string SelectedUserId { get; set; }
        public string SelectedRoleId { get; set; }

        // Permissions
        public List<RoleFormPermission> Permissions { get; set; }

        public List<SelectListItem> RoleList { get; set; }
        public List<SelectListItem> FormList { get; set; }

        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}
