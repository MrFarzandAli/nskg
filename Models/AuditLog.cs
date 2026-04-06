namespace Nskg.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public string UserName { get; set; }
        public string Action { get; set; }   // Create, Update, Delete, Error
        public string TableName { get; set; }
        public string RecordId { get; set; }

        public string Details { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
