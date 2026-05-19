namespace Nskg.Models.ViewModels
{
    public class ChallanDetailVM
    {
        public decimal? Qty { get; set; }
        public string? BillTiNo { get; set; }  // Changed from decimal? to string?
        public decimal? BillTiAmt { get; set; }
        public decimal? PaidAmt { get; set; }
        public string? CusName { get; set; }
        public string? SendTo { get; set; }
        public string? DCNo { get; set; }
        public string? BilNo { get; set; }
    }
}