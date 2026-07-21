namespace Nskg.Models
{
    public class CommHead
    {
        public long Id { get; set; }

        public string DocNo { get; set; } = null!;
        public DateTime DocDate { get; set; }

        public int? ChalNo { get; set; }
        public string? Station { get; set; }
        public string? VehicleNo { get; set; }
        public string? Driver { get; set; }
        public string? Transporter { get; set; }
        public string? TransCode { get; set; }

        public int? StationId { get; set; }
        public int? TransId { get; set; }
        public int? AdvanceId { get; set; }

        public decimal? Labour { get; set; }
        public decimal? NetAmt { get; set; }
        public string? Narration { get; set; }

        public decimal? DeliveryAmt { get; set; }
        public decimal? LocalAmt { get; set; }

        public string? Advance { get; set; }

        public decimal? StationAmt { get; set; }
        public decimal? TransporterAmt { get; set; }
        public decimal? AdvanceAmt { get; set; }
        public decimal? TotAmt { get; set; }

        public string? AcCode { get; set; }

        public decimal? PartyExAmt { get; set; }
        public decimal? Lifter2Amt { get; set; }
        public decimal? OtherExAmt { get; set; }
        public decimal? TotNet { get; set; }

        public string? StationCode { get; set; }

        public decimal? DeliveryAmt1 { get; set; }

        public string? AdvanceCode { get; set; }

        public decimal? Tax { get; set; }

        public string? AcCode1 { get; set; }
        public string? AcCode2 { get; set; }

        public string? AcType { get; set; }
        public string? AcType1 { get; set; }
        public string? AcType2 { get; set; }

        public decimal? BillTiAmt { get; set; }

        public bool DescYn { get; set; }

        public string? Acc1 { get; set; }
        public string? Acc2 { get; set; }

        public string? AccName1 { get; set; }
        public string? AccName2 { get; set; }

        public decimal? AccAmt1 { get; set; }
        public decimal? AccAmt2 { get; set; }

        public string? AccAcType1 { get; set; }
        public string? AccAcType2 { get; set; }

        public string? AccCType1 { get; set; }
        public string? AccCType2 { get; set; }

        public bool DescYn1 { get; set; }

        public decimal? PaidAmt { get; set; }

        public decimal? DeliveryAmt2 { get; set; }

        // Common ERP Fields
        public int CompanyId { get; set; }
        public int FinancialYearId { get; set; }

        public DateTime CreatedOn { get; set; }
        public string? CreatedBy { get; set; } = null!;

        public DateTime? ModifiedOn { get; set; }
        public string? ModifiedBy { get; set; }

        // Soft delete flag
        public bool IsDeleted { get; set; }

        // Navigation
        public virtual ICollection<CommDetail> Details { get; set; }
            = new List<CommDetail>();
    }
}
