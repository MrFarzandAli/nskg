using Microsoft.AspNetCore.Mvc.Rendering;

namespace Nskg.Models.ViewModels
{   
    public class CompanySelectionViewModel
    {
        public int SelectedCompanyId { get; set; }
        public int SelectedFinancialYearId { get; set; }

        public List<SelectListItem> Companies { get; set; }
        public List<SelectListItem> FinancialYears { get; set; }
    }
}
