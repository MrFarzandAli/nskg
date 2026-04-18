namespace Nskg.Models
{
    public class VoucherTypeSetting
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }

        public bool IsBankRequired { get; set; }
        public bool AllowMultiLine { get; set; }
        public bool AutoBalance { get; set; }
    }
}
