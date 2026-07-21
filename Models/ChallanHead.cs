namespace Nskg.Models
{
    public class ChallanHead
    {
        public int Id { get; set; } // PK

        public int FyId { get; set; }
        public int CompanyId { get; set; }

        public int StationId { get; set; }
        public int TransId { get; set; }

        public string? DocNo { get; set; }
        public DateTime? DocDate { get; set; }
        public int? ChalNo { get; set; }

        public string? Station { get; set; }
        public string? VehicleNo { get; set; }
        public string? Driver { get; set; }

        public string? Transporter { get; set; }
        public string? TransCode { get; set; }
        public decimal? TotBillTi { get; set; }
        public decimal? NetAmt { get; set; }

        public string? Narration { get; set; }
        public string? CoCode { get; set; }

        public decimal? DeliveryAmt { get; set; }
        public decimal? LocalAmt { get; set; }
        public decimal? TotPaid { get; set; }

        public string? DescYn { get; set; }

        public decimal? TotToPaid { get; set; }
        public decimal? TotPartyEx { get; set; }
        public decimal? TotLifter2 { get; set; }
        public decimal? TotOtherEx { get; set; }

        public string? StationCode { get; set; }

        public decimal? BillTiAmt { get; set; }

        public decimal? DeliveryAmt2 { get; set; }
        public string? OtherEx2 { get; set; }
        public string? LocalAmt2 { get; set; }
        public string? PartyEx2 { get; set; }

        public string? PExpCode { get; set; }
        public string? PExpCode2 { get; set; }
        public string? PExpCode3 { get; set; }
        public decimal? PExpAmt { get; set; }
        public decimal? PExpAmt2 { get; set; }
        public decimal? PExpAmt3 { get; set; }

        public decimal? PExpBilti { get; set; }
        public decimal? PExpBilti2 { get; set; }
        public decimal? PExpBilti3 { get; set; }

        public string? PartyStationCode { get; set; }

        // ✅ Soft Delete
        public bool IsDeleted { get; set; }

        // ✅ Audit Fields (optional for future use)
        public DateTime CreatedOn { get; set; }
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
        public string? ModifiedBy { get; set; }

        public long? commBookId { get; set; }
        // 🔗 Navigation
        public List<ChallanDet> Details { get; set; } = new();
    }
}
