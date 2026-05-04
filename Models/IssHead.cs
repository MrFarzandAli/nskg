using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nskg.Models
{
    

    [Table("ISSHEAD")]
    public class IssHead
    {
        [Key]
        public int Id { get; set; }

        public int CompanyId { get; set; }

        public int FyId { get; set; }
        public int CustomerId { get; set; }
        public int StationId { get; set; }

        [StringLength(3)]
        public string? BCode { get; set; }

        [StringLength(1)]
        public string? Cancel { get; set; }

        public DateTime? DocDate { get; set; }

        [StringLength(3)]
        public string? WCode { get; set; }

        [Required]
        [StringLength(10)]
        public string DocNo { get; set; }

        [StringLength(9)]
        public string? CusCode { get; set; }

        [StringLength(25)]
        public string? RefDocNo { get; set; }

        public DateTime? RefDocDate { get; set; }

        public long? InvNo { get; set; }

        public DateTime? InvDate { get; set; }

        [StringLength(2)]
        public string? CoCode { get; set; }

        public decimal? MAmount { get; set; }

        public decimal? Discount { get; set; }

        public decimal? DisAmt { get; set; }

        public decimal? STax { get; set; }

        public decimal? STaxAmt { get; set; }

        public decimal? NetAmt { get; set; }

        [StringLength(3)]
        public string? SCode { get; set; }

        [StringLength(6)]
        public string? AccCode { get; set; }

        [StringLength(1)]
        public string? AcType { get; set; }

        [StringLength(100)]
        public string? CusName { get; set; }

        [StringLength(120)]
        public string? CusAdd { get; set; }

        [StringLength(15)]
        public string? CusTel { get; set; }

        public decimal? Qty { get; set; }

        [StringLength(100)]
        public string? Narration { get; set; }

        [StringLength(450)]
        public string? UserId { get; set; }

        [StringLength(25)]
        public string? STaxNo { get; set; }

        [StringLength(15)]
        public string? NTN { get; set; }

        [StringLength(20)]
        public string? VehicleNo { get; set; }

        [StringLength(6)]
        public string? Transporter { get; set; }

        [StringLength(100)]
        public string? TrName { get; set; }

        [StringLength(100)]
        public string? DCNO { get; set; }

        public decimal? CommPer { get; set; }

        [StringLength(1)]
        public string? DescYN { get; set; }

        public DateTime? DueDate { get; set; }

        public decimal? WH_IT { get; set; }

        public decimal? WH_IT_Amt { get; set; }

        public decimal? WH_ST { get; set; }

        public decimal? WH_ST_Amt { get; set; }

        [StringLength(6)]
        public string? WH_IT_Acc { get; set; }

        [StringLength(6)]
        public string? WH_ST_Acc { get; set; }

        public DateTime? WH_ST_Rec { get; set; }

        public DateTime? WH_IT_Rec { get; set; }

        [StringLength(6)]
        public string? Transporter2 { get; set; }

        [StringLength(6)]
        public string? Transporter3 { get; set; }

        [StringLength(100)]
        public string? TrName2 { get; set; }

        [StringLength(100)]
        public string? TrName3 { get; set; }

        public decimal? Cartage1 { get; set; }

        public decimal? Cartage2 { get; set; }

        public decimal? Cartage3 { get; set; }

        [StringLength(10)]
        public string? CartType { get; set; }

        [StringLength(10)]
        public string? CartType2 { get; set; }

        [StringLength(10)]
        public string? CartType3 { get; set; }

        [StringLength(100)]
        public string? Fooder { get; set; }

        [StringLength(100)]
        public string? SendTo { get; set; }

        [StringLength(15)]
        public string? PType { get; set; }

        public decimal? Labour { get; set; }

        public decimal? T_T { get; set; }

        public decimal? BilNo { get; set; }

        public decimal? BillTiNo { get; set; }

        public decimal? PartyEx { get; set; }

        public decimal? Lifter2 { get; set; }

        public decimal? OtherEx { get; set; }

        [StringLength(6)]
        public string? FooderCode { get; set; }

        [StringLength(100)]
        public string? IName { get; set; }

        [StringLength(1)]
        public string? DescYN1 { get; set; }
        // ✅ Soft Delete
        public bool IsDeleted { get; set; }

        // ✅ Audit Fields
        public DateTime CreatedOn { get; set; }
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
        public string? ModifiedBy { get; set; }
        // Navigation Property
        public ICollection<IssDetail> Details { get; set; } = new List<IssDetail>();

        public GLChart3? Station { get; set; }
        public GLChart3? Customer { get; set; }

    }
}
