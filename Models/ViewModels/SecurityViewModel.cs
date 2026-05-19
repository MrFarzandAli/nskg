//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Mvc.Rendering;
//using Nskg.Models.Security;

//namespace Nskg.Models.ViewModels
//{
//    public class SecurityViewModel
//    {
//        // Roles
//        public List<IdentityRole> Roles { get; set; }
//        public string NewRoleName { get; set; }

//        // Forms
//        public List<Form> Forms { get; set; }
//        public Form NewForm { get; set; }

//        // Users
//        public List<IdentityUser> Users { get; set; }
//        public string SelectedUserId { get; set; }
//        public string SelectedRoleId { get; set; }

//        // Permissions
//        public List<RoleFormPermission> Permissions { get; set; }

//        public List<SelectListItem> RoleList { get; set; }
//        public List<SelectListItem> FormList { get; set; }

//        public bool CanView { get; set; }
//        public bool CanCreate { get; set; }
//        public bool CanEdit { get; set; }
//        public bool CanDelete { get; set; }
//    }
//}
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nskg.Models.Security;

namespace Nskg.Models.ViewModels
{
    public class SecurityViewModel
    {
        // For Add Role
        public string NewRoleName { get; set; }

        // For Add Form
        public Form NewForm { get; set; }

        // For Assign Role to User
        public string SelectedUserId { get; set; }
        public string SelectedRoleId { get; set; }

        // Data Lists - Using Security namespace
        public List<IdentityRole> Roles { get; set; }
        public List<Nskg.Models.Security.Form> Forms { get; set; }              // ← FIXED
        public List<IdentityUser> Users { get; set; }
        public List<Nskg.Models.Security.RoleFormPermission> Permissions { get; set; }  // ← FIXED

        // For Dropdowns
        public List<SelectListItem> RoleList { get; set; }
        public List<SelectListItem> FormList { get; set; }

        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}