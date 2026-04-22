namespace Nskg.Models
{
    public class GLTrans
    {
        public int Id { get; set; }

        public string? DocNo { get; set; }
        public DateTime Docdate { get; set; }

        public string Accode { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }

        public string? ContraAcc { get; set; }
        public string? Votype { get; set; }
        public string? RefType { get; set; }
        public int? RefId { get; set; }
        
        public int LineNo { get; set; }

        public int CompanyId { get; set; }
        public int FinancialYearId { get; set; }

        public string? Narration { get; set; }
    }
}
