namespace Nskg.Models.Security
{
    public class RoleFormPermission
    {
        public int Id { get; set; }

        public string RoleId { get; set; }
        public int FormId { get; set; }

        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}
