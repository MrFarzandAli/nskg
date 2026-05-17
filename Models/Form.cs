//namespace Nskg.Models
//{
//    public class Form
//    {
//    }
//}
using System.ComponentModel.DataAnnotations;

namespace Nskg.Models
{
    public class Form
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Controller { get; set; }
        public string Action { get; set; }
    }
}