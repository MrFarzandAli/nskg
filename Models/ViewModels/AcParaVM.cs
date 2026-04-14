namespace Nskg.Models.ViewModels
{
    public class AcParaVM
    {
        public string ParentCode { get; set; }
        public string ChildCode { get; set; }
        public string AccountType { get; set; }

        public List<AcPara> ParentList { get; set; }
        public List<AcPara> ChildList { get; set; }
    }
}
