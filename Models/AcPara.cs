using System.ComponentModel.DataAnnotations.Schema;

namespace Nskg.Models
{
    public class AcPara
    {
        public int Id { get; set; }
        public int? GLChart1Id { get; set; }   // for Parent = P
        public int? GLChart3Id { get; set; }   // for Parent = C
        public string? Accode { get; set; }   // 6 digit code
        [Column("ACTYPE")]
        public string ActypeCode { get; set; }
        public string? Acname { get; set; }   // Account Name
        public string? Cocode { get; set; }   // Company Code
        public int CompanyId { get; set; }
        public decimal? Opening { get; set; }
        public string? Parent { get; set; }   // 'P' or 'C'
        public bool IsDeleted { get; set; }

        // ✅ Audit Fields
        public DateTime CreatedOn { get; set; }
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public GLChart1? GLChart1 { get; set; }
        public GLChart3? GLChart3 { get; set; }

        [ForeignKey(nameof(ActypeCode))]
        public virtual Actype Actype { get; set; }

    }
}
