namespace Nskg.Models
{
    public class ChallanDet
    {
        public int Id { get; set; } // PK

        public int FyId { get; set; }
        public int CompanyId { get; set; }
        public int ChallanHeadId { get; set; }

        public string? DocNo { get; set; } // FK reference

        public string? BCode { get; set; }
        public string? Cnacel { get; set; }

        public DateTime? DocDate { get; set; }

        public string? WCode { get; set; }
        public string? CusCode { get; set; }

        public string? RefDocNo { get; set; }
        public DateTime? RefDocDate { get; set; }

        public long? InvNo { get; set; }
        public DateTime? InvDate { get; set; }

        public string? CoCode { get; set; }

        public decimal? MAmount { get; set; }
        public decimal? Discount { get; set; }
        public decimal? DisAmt { get; set; }

        public decimal? STax { get; set; }
        public decimal? STaxAmt { get; set; }

        public decimal? NetAmt { get; set; }

        public string? SCode { get; set; }
        public string? AccCode { get; set; }
        public string? AcType { get; set; }

        public string? CusName { get; set; }
        public string? CusAdd { get; set; }
        public string? CusTel { get; set; }
        public int? Qty { get; set; }

        public string? STaxNo { get; set; }
        public string? NTN { get; set; }
        public string? VehicleNo { get; set; }

        public string? Transporter { get; set; }
        public string? TrName { get; set; }

        public string? DCNo { get; set; }

        public decimal? CommPer { get; set; }

        public string? Narration { get; set; }
        public string? DescYn { get; set; }

        public DateTime? DueDate { get; set; }

        public decimal? WhIt { get; set; }
        public decimal? WhItAmt { get; set; }

        public decimal? WhSt { get; set; }
        public decimal? WhStAmt { get; set; }

        public string? WhItAcc { get; set; }
        public string? WhStAcc { get; set; }

        public DateTime? WhStRec { get; set; }
        public DateTime? WhItRec { get; set; }

        public string? UserId { get; set; }

        public string? Transporter2 { get; set; }
        public string? Transporter3 { get; set; }

        public string? TrName2 { get; set; }
        public string? TrName3 { get; set; }

        public decimal? Cartage1 { get; set; }
        public decimal? Cartage2 { get; set; }
        public decimal? Cartage3 { get; set; }

        public string? CartType { get; set; }
        public string? CartType2 { get; set; }
        public string? CartType3 { get; set; }

        public string? Fooder { get; set; }
        public string? SendTo { get; set; }

        public string? PType { get; set; }

        public decimal? Labour { get; set; }
        public decimal? TT { get; set; }

        public string? BilNo { get; set; }
        public string? BillTiNo { get; set; }
        public decimal? BillTiAmt { get; set; }

        public decimal? PaidAmt { get; set; }
        public decimal? ToPaidAmt { get; set; }

        public decimal? PartyEx { get; set; }
        public decimal? Lifter2 { get; set; }
        public decimal? OtherEx { get; set; }

        public DateTime? BillTiDate { get; set; }

        // 🔗 Navigation
        public ChallanHead ChallanHead { get; set; }
    }
}
