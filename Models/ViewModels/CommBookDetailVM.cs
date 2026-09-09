namespace Nskg.Models.ViewModels
{
    public class CommBookDetailVM
    {
        public string? BillTiNo { get; set; }
        public string? Fooder { get; set; }
        public string? CusName { get; set; }
        public string? SendTo { get; set; }
        public decimal? BillTiAmt { get; set; }
        public decimal? PaidAmt { get; set; }
        public decimal? ToPaidAmt { get; set; }
        public decimal? DeliveryAmt { get; set; }
        public decimal? LocalAmt { get; set; }
        public decimal? NetAmt { get; set; }

        // Added properties to match PL/SQL / server expectations
        public string? DCNo { get; set; }
        public string? BilNo { get; set; }
        public decimal? Qty { get; set; }
        public decimal? PartyEx { get; set; }
        public decimal? Lifter2 { get; set; }
        public decimal? OtherEx { get; set; }
        public string? FooderCode { get; set; }
        public string? Transporter { get; set; }
        public string? TrName { get; set; }
        public int? ChallanId { get; set; }
    }
}