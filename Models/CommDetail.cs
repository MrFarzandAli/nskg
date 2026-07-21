namespace Nskg.Models
{
    public class CommDetail
    {
        public long Id { get; set; }

        public long CommHeadId { get; set; }

        public string? BCode { get; set; }
        public bool? Cancel { get; set; }

        public DateTime? DocDate { get; set; }

        public string? WCode { get; set; }

        public string? CusCode { get; set; }

        public string? RefDocNo { get; set; }
        public DateTime? RefDocDate { get; set; }

        public long? InvNo { get; set; }
        public DateTime? InvDate { get; set; }

        public decimal? MAmount { get; set; }

        public decimal? Discount { get; set; }
        public decimal? DisAmt { get; set; }

        public decimal? STax { get; set; }
        public decimal? STaxAmt { get; set; }

        public decimal? NetAmt { get; set; }

        public string? SCode { get; set; }

        public string? AcCode { get; set; }
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

        public string? DcNo { get; set; }

        public decimal? CommPer { get; set; }

        public string? Narration { get; set; }

        public bool? DescYn { get; set; }

        public DateTime? DueDate { get; set; }

        public decimal? WhIT { get; set; }
        public decimal? WhITAmt { get; set; }

        public decimal? WhST { get; set; }
        public decimal? WhSTAmt { get; set; }

        public string? WhITAcc { get; set; }
        public string? WhSTAcc { get; set; }

        public DateTime? WhSTRec { get; set; }
        public DateTime? WhITRec { get; set; }

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

        public int? BilNo { get; set; }
        public int? BillTiNo { get; set; }

        public decimal? BillTiAmt { get; set; }

        public decimal? PaidAmt { get; set; }
        public decimal? ToPaidAmt { get; set; }

        public decimal? DeliveryAmt { get; set; }
        public decimal? LocalAmt { get; set; }

        public decimal? PartyExAmt { get; set; }
        public decimal? Lifter2Amt { get; set; }
        public decimal? OtherExAmt { get; set; }

        public string? FooderCode { get; set; }

        public decimal? DeliveryAmt2 { get; set; }

        // Common ERP Fields
        public int CompanyId { get; set; }
        public int FinancialYearId { get; set; }

        public DateTime CreatedOn { get; set; }
        public string CreatedBy { get; set; } = null!;

        public DateTime? ModifiedOn { get; set; }
        public string? ModifiedBy { get; set; }

        // Navigation
        public virtual CommHead CommHead { get; set; } = null!;
    }
}
