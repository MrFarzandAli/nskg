namespace Nskg.Models
{
    public class CompanyCookieContext
    {
        public int CompanyId { get; set; }
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public int FinancialYearId { get; set; }
    }
}
