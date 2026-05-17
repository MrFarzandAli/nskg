//namespace Nskg.Models
//{
//    public class RoleFormPermission
//    {
//    }
//}
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Nskg.Models
{
    public class RoleFormPermission
    {
        [Key]
        public int Id { get; set; }
        public string RoleId { get; set; }
        public int FormId { get; set; }
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }

        // Navigation properties
        public virtual IdentityRole Role { get; set; }
        public virtual Form Form { get; set; }
    }
}