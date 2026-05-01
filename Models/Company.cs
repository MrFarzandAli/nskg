namespace Nskg.Models
{
    public class Company
    {
        public int Id { get; set; }
        public string? Cocode { get; set; }
        public string Name { get; set; }
        public string Mobile { get; set; }
        public string Email { get; set; }

        public bool IsActive { get; set; }

        // ✅ Soft Delete
        public bool IsDeleted { get; set; }

        // ✅ Audit Fields
        public DateTime CreatedOn { get; set; }
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
        public string? ModifiedBy { get; set; }
    }
}
