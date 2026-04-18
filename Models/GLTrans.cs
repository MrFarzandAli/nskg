namespace Nskg.Models
{
    public class GLTrans
    {
        public int Id { get; set; }

        public string Vono { get; set; }
        public DateTime Vodate { get; set; }

        public string Accode { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }

        public int CompanyId { get; set; }
        public int FinancialYearId { get; set; }

        public string? Narration { get; set; }
    }
}
