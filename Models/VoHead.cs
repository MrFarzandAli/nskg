namespace Nskg.Models
{
    public class VoHead
    {
        public int Id { get; set; } // Added PK for EF

        public string? Vono { get; set; }
        public DateTime? Vodate { get; set; }
        public string? Votype { get; set; }

        public decimal? Totdramt { get; set; }
        public decimal? Totcramt { get; set; }
        public string? Narration { get; set; }
        public string? Cancel { get; set; }
        public int? Entries { get; set; }

        public string? Cocode { get; set; }   // FK → Company

        public string? Invno { get; set; }
        public DateTime? Invdate { get; set; }

        public string? Ac1 { get; set; }
        public string? Ac2 { get; set; }
        public string? Ac3 { get; set; }
        public decimal? Hdramt { get; set; }
        public decimal? Hcramt { get; set; }

        public string? Actype { get; set; }
        public decimal? Diff { get; set; }

        public string? Haccode { get; set; }

        public string? Vono2 { get; set; }
        public string? VoucType { get; set; }

        public DateTime? EntryDate { get; set; }
        public string? PersonName { get; set; }
        public string? Userid { get; set; }

        public DateTime? ExpDate { get; set; }
        public int? ExpDays { get; set; }

        public string? Allow { get; set; }

        public decimal? Totptax { get; set; }

        public string? Partycode { get; set; }

        public int CompanyId { get; set; }
        public int FinancialYearId { get; set; }
        public int? gl3Id { get; set; }

        public string Status { get; set; } = "Draft";

        // 🔗 RELATIONS
        public List<VoDet>? Details { get; set; }
        public GLChart3? gl3 { get; set; }
    }
}
