namespace Nskg.Models
{
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Mobile { get; set; }
        public string Email { get; set; }

        public bool IsActive { get; set; }
    }
}
