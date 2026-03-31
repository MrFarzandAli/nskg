using Microsoft.AspNetCore.Mvc.Rendering;

namespace Nskg.Models.ViewModels
{
    public class AssignRoleViewModel
    {
        public string UserId { get; set; }
        public string Email { get; set; }

        public List<SelectListItem> Roles { get; set; }

        public string SelectedRole { get; set; }
    }
}
