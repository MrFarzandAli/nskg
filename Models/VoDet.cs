namespace Nskg.Models
{
    public class VoDet
    {
        public int Id { get; set; } // Added PK

        public int VoHeadId { get; set; }

        public string? Vono { get; set; }
        public DateTime? Vodate { get; set; }
        public string? Votype { get; set; }

        public string? Ac1 { get; set; }
        public string? Ac2 { get; set; }
        public string? Ac3 { get; set; }

        public string? Actype { get; set; }

        public decimal? Dramt { get; set; }
        public decimal? Cramt { get; set; }

        public string? Narration { get; set; }
        public string? Cancel { get; set; }

        public decimal? Pay { get; set; }

        public string? Cocode { get; set; }

        public string? Invno { get; set; }
        public DateTime? Invdate { get; set; }

        public string? Acc { get; set; }

        public string? Ndramt { get; set; }
        public string? Ncramt { get; set; }

        public string? Hacc { get; set; }

        public string? Name { get; set; }

        public string? Chqno { get; set; }
        public string? Refno { get; set; }

        public decimal? Qty { get; set; }
        public decimal? Rate { get; set; }

        public string? Wcode { get; set; }

        public decimal? Qtyin { get; set; }
        public decimal? Qtyout { get; set; }

        public string? Ctype { get; set; }

        public DateTime? ChqDate { get; set; }

        public DateTime? EntryDate { get; set; }
        public string? Userid { get; set; }

        public decimal? FAmt { get; set; }
        public decimal? ERate { get; set; }

        public string? Currency { get; set; }

        public DateTime? ExpDate { get; set; }

        public decimal? Discount { get; set; }
        public decimal? Payment { get; set; }

        public int? CrDays { get; set; }

        public string? Allow { get; set; }

        public int? Billtino { get; set; }
        public int? Bilno { get; set; }

        public string? Vehicleno { get; set; }

        public string? Transcode { get; set; }

        public decimal? Ptax { get; set; }

        public string? Transporter { get; set; }

        public int? gl3Id { get; set; }
        // ✅ Soft Delete
        public bool IsDeleted { get; set; }

        // ✅ Audit Fields
        public DateTime CreatedOn { get; set; }
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }
        public string? ModifiedBy { get; set; }
        // 🔗 NAVIGATION
        public VoHead? VoHead { get; set; }
        public GLChart3? gl3 { get; set; }
    }
}
