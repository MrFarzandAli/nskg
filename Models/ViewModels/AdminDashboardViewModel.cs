using System;
using System.Collections.Generic;

namespace Nskg.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        // Filter options
        public int SelectedCompanyId { get; set; }
        public int SelectedFyId { get; set; }
        public string SelectedCompanyName { get; set; } = "All Companies / Branches";
        public string SelectedFyName { get; set; } = string.Empty;
        public List<DashboardCompanyItem> Companies { get; set; } = new();
        public List<DashboardFyItem> FinancialYears { get; set; } = new();

        // 1. Operations & Logistics KPI Cards
        public int TodayBiltiesCount { get; set; }
        public decimal TodayFreightBooked { get; set; }

        public int MonthBiltiesCount { get; set; }
        public decimal MonthFreightBooked { get; set; }

        public int TotalChallansCount { get; set; }
        public int ActiveVehiclesCount { get; set; }

        public int PendingToPayCount { get; set; }
        public decimal PendingToPayAmount { get; set; }

        public decimal PaidFreightAmount { get; set; }
        public decimal ToPayFreightAmount { get; set; }

        // 2. Charts Data
        public List<string> MonthlyLabels { get; set; } = new();
        public List<decimal> MonthlyFreight { get; set; } = new();
        public List<int> MonthlyBilties { get; set; } = new();

        public List<string> StationNames { get; set; } = new();
        public List<decimal> StationFreights { get; set; } = new();
        public List<int> StationBiltyCounts { get; set; } = new();

        public List<string> TopBookingParties { get; set; } = new();
        public List<decimal> TopBookingFreights { get; set; } = new();

        // 3. Feeds
        public List<RecentBiltyItem> RecentBilties { get; set; } = new();
        public List<RecentChallanItem> RecentChallans { get; set; } = new();
    }
}
