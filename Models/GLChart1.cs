using System.ComponentModel.DataAnnotations;

namespace Nskg.Models
{
    public class GLChart1
    {
        public int Id { get; set; }   // PK

        [Required]
        [StringLength(3)]
        public string? AC1 { get; set; }

        [Required]
        [StringLength(50)]
        public string? Name { get; set; }

        public string? AcType { get; set; }
        public string? IncBal { get; set; }

        public decimal? Opening { get; set; }

        public string? CoCode { get; set; }
        public string? AType { get; set; }
        public string? CType { get; set; }

        public int? PLSQ { get; set; }
        public int? BSSQ { get; set; }

        public string? ACCCode { get; set; }
        public decimal? PrevBal { get; set; }

        public string? SubCatCode { get; set; }

        // 🔗 Navigation
        public ICollection<GLChart3>? GLChart3s { get; set; }
        public List<AcPara>? acParas { get; set; }
    }
}
