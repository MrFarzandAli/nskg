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
        // ✅ Soft Delete
        public bool IsDeleted { get; set; }

        // ✅ Audit Fields
        public DateTime CreatedOn { get; set; }
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
        public string? ModifiedBy { get; set; }
    }

}
