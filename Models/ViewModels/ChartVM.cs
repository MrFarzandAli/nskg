using Microsoft.AspNetCore.Mvc.Rendering;

namespace Nskg.Models.ViewModels
{
    public class ChartVM
    {
        public List<GLChart1> Accounts { get; set; }
        public List<GLChart3> Details { get; set; }
        public List<SelectListItem> Categories { get; set; }
    }
}
