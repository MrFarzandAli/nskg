using System.ComponentModel.DataAnnotations;

namespace Nskg.Models
{
    public class OpeningBalance
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        public int FinancialYearId { get; set; }

        [StringLength(20)]
        public string Accode { get; set; }

        public decimal Debit { get; set; }

        public decimal Credit { get; set; }
    }
}
