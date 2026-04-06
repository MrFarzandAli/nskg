using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nskg.Models
{
    [Table("ACCCAT", Schema = "NSKG")]
    public class AccCat
    {
        [Key]
        [Column("CATCODE")]
        [StringLength(2)]
        public string CatCode { get; set; }

        [Column("CATEGORY")]
        [StringLength(20)]
        public string Category { get; set; }

        [Column("COCODE")]
        [StringLength(2)]
        public string CoCode { get; set; }
    }
    
}
