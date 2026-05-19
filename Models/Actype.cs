using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nskg.Models
{

    [Table("ACTYPE")]
    public class Actype
    {
        [Key] // assuming ACTYPE is primary key (confirm if needed)
        public int Id { get; set; }

        [Column("ACTYPE")]
        [StringLength(2)]
        public string ACTYPE { get; set; }

        [Column("ACNAME")]
        [StringLength(30)]
        public string ACNAME { get; set; }

        [Column("COCODE")]
        [StringLength(2)]
        public string? COCODE { get; set; }
        // ✅ Soft Delete
        public bool IsDeleted { get; set; }

        // ✅ Audit Fields
        public DateTime CreatedOn { get; set; }
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public List<AcPara>? acParas { get; set; }
    }
}
