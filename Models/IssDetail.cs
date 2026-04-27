using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nskg.Models
{


    [Table("ISSDETAIL")]
    public class IssDetail
    {
        [Key]
        public int Id { get; set; }

        public int CompanyId { get; set; }

        public int FyId { get; set; }

        public int IssHeadId { get; set; }

        [ForeignKey("IssHeadId")]
        public IssHead IssHead { get; set; }

        [StringLength(3)]
        public string? BCode { get; set; }

        [StringLength(3)]
        public string? WCode { get; set; }

        [StringLength(10)]
        public string? DocNo { get; set; }

        public DateTime? DocDate { get; set; }

        [StringLength(9)]
        public string? CusCode { get; set; }

        [StringLength(1)]
        public string? IType { get; set; }

        [StringLength(6)]
        public string? ItemCode { get; set; }

        [StringLength(6)]
        public string? Unit { get; set; }

        public decimal? Qty { get; set; }

        public decimal? Rate { get; set; }

        public decimal? Amount { get; set; }

        [StringLength(3)]
        public string? SCode { get; set; }

        [StringLength(9)]
        public string? AccCode { get; set; }

        [StringLength(25)]
        public string? RefDocNo { get; set; }

        public DateTime? RefDocDate { get; set; }

        public decimal? Packs { get; set; }

        public decimal? QtyPerPack { get; set; }

        [StringLength(50)]
        public string? IName { get; set; }

        public decimal? STax { get; set; }

        public decimal? STaxAmt { get; set; }

        public decimal? AmtNet { get; set; }

        [StringLength(450)]
        public string? UserId { get; set; }

        public DateTime? InvDate { get; set; }

        [StringLength(15)]
        public string? InvNo { get; set; }

        [StringLength(50)]
        public string? CusName { get; set; }

        public decimal? Comm { get; set; }

        public decimal? PCommPer { get; set; }

        public decimal? Comm2 { get; set; }

        [StringLength(15)]
        public string? DescCode { get; set; }

        public decimal? Comm3 { get; set; }

        [StringLength(100)]
        public string? Fooder { get; set; }

        [StringLength(2)]
        public string? FormulaCode { get; set; }

        public decimal? Freight { get; set; }

        public decimal? Amount1 { get; set; }

        public decimal? TotAmt { get; set; }

        public decimal? Discount { get; set; }

        [StringLength(100)]
        public string? PackingN { get; set; }

        [StringLength(20)]
        public string? VehicleNo { get; set; }

        public decimal? BillTiNo { get; set; }

        public decimal? BilNo { get; set; }
    }
}
