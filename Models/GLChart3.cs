using System.ComponentModel.DataAnnotations;

namespace Nskg.Models
{
    public class GLChart3
    {
        public int Id { get; set; }   // PK

        // 🔥 NEW FK
        public int GLChart1Id { get; set; }

        [Required]
        public string? AC1 { get; set; }   // optional (for legacy/reference)
        public string? AC2 { get; set; }       

        [Required]
        public string? AC3 { get; set; }

        [Required]
        public string? Name { get; set; }

        public string? AcType { get; set; }
        public string? IncBal { get; set; }

        public decimal? Opening { get; set; }

        public string? Plot { get; set; }
        public string? Street { get; set; }
        public string? Area { get; set; }
        public string? City { get; set; }

        public string? Phone1 { get; set; }
        public string? Phone2 { get; set; }
        public string? Phone3 { get; set; }
        public string? Mobile { get; set; }

        public string? Email { get; set; }
        public string? Fax { get; set; }

        public string? CoCode { get; set; }
        public string? STaxNo { get; set; }

        public string? CName { get; set; }

        public string? Add1 { get; set; }
        public string? Add2 { get; set; }
        public string? Add3 { get; set; }

        public string? CType { get; set; }

        public decimal? Rate { get; set; }
        public string? Unit { get; set; }

        public string? SCode { get; set; }

        public decimal? Closing { get; set; }
        public string? NTN { get; set; }

        public decimal? Rate2 { get; set; }
        public decimal? SPer { get; set; }
        public decimal? PRate { get; set; }

        public string? ACC { get; set; }

        public decimal? PrevBal { get; set; }

        public string? CHName { get; set; }

        public int? CRDays { get; set; }

        public decimal? CommPer { get; set; }

        public string? CommExp { get; set; }
        public string? CommExp2 { get; set; }
        public string? CommExp3 { get; set; }

        public decimal? WH_IT { get; set; }
        public decimal? WH_ST { get; set; }

        public string? WH_IT_ACC { get; set; }
        public string? WH_ST_ACC { get; set; }

        public decimal? OPComm { get; set; }
        public decimal? OPComm2 { get; set; }
        public decimal? OPComm3 { get; set; }

        public decimal? TR_DRate { get; set; }
        public decimal? TR_LRate { get; set; }
        public decimal? TR_RRate { get; set; }

        public decimal? Bharti { get; set; }

        public decimal? CurrBill { get; set; }
        public decimal? CurrRec { get; set; }

        // 🔗 Navigation
        public GLChart1? GLChart1 { get; set; }
        public List<AcPara>? acParas { get; set; }

    }
}
