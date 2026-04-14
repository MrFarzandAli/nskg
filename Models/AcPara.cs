namespace Nskg.Models
{
    public class AcPara
    {
        public int Id { get; set; }
        public string? Accode { get; set; }   // 6 digit code
        public string? Actype { get; set; }   // Account Type
        public string? Acname { get; set; }   // Account Name
        public string? Cocode { get; set; }   // Company Code
        public decimal? Opening { get; set; }
        public string? Parent { get; set; }   // 'P' or 'C'
    }
}
