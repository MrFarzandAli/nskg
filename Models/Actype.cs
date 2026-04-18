using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nskg.Models
{

    [Table("ACTYPE")]
    public class Actype
    {
        [Key] // assuming ACTYPE is primary key (confirm if needed)
        [Column("ACTYPE")]
        [StringLength(1)]
        public string ACTYPE { get; set; }

        [Column("ACNAME")]
        [StringLength(30)]
        public string ACNAME { get; set; }

        [Column("COCODE")]
        [StringLength(2)]
        public string? COCODE { get; set; }

        public List<AcPara>? acParas { get; set; }
    }
}
