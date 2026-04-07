using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
namespace Nskg.Models.ViewModels
{   
    public class CompanySelectionViewModel
    {

        [Required(ErrorMessage = "Please select a company")]
        public int? SelectedCompanyId { get; set; }

        [Required(ErrorMessage = "Please select a financial year")]
        public int? SelectedFinancialYearId { get; set; }

        public List<SelectListItem> Companies { get; set; }
        public List<SelectListItem> FinancialYears { get; set; }
    }
}
