using System;
using System.Collections.Generic;

namespace Nskg.Models.ViewModels
{
    public class FinancialDashboardViewModel
    {
        // Filter info
        public int SelectedCompanyId { get; set; }
        public int SelectedFyId { get; set; }
        public string SelectedCompanyName { get; set; } = "All Companies";
        public string SelectedFyName { get; set; } = string.Empty;
        public List<DashboardCompanyItem> Companies { get; set; } = new();
        public List<DashboardFyItem> FinancialYears { get; set; } = new();

        // 1. Core Financial Balances (High-Priority Big KPI Cards)
        public decimal CashBalance { get; set; }
        public decimal BankBalance { get; set; }
        public decimal TotalReceivables { get; set; }
        public decimal TotalPayables { get; set; }
        public decimal PendingToPayAmount { get; set; }
        public int PendingToPayCount { get; set; }
        public decimal DriverAdvanceBalance { get; set; }

        // 2. Operational & Transport KPIs
        public decimal TotalFreightBooked { get; set; }
        public int TotalBiltiesCount { get; set; }
        public int TotalChallansCount { get; set; }
        public int ActiveVehiclesCount { get; set; }
        public decimal TotalNetCommission { get; set; }
        public decimal PaidFreightTotal { get; set; }
        public decimal ToPayFreightTotal { get; set; }

        // 3. Analytics Charts Data
        public List<string> MonthlyLabels { get; set; } = new();
        public List<decimal> MonthlyFreight { get; set; } = new();
        public List<int> MonthlyBilties { get; set; } = new();

        public List<string> StationNames { get; set; } = new();
        public List<decimal> StationFreights { get; set; } = new();

        public List<string> TopPartyNames { get; set; } = new();
        public List<decimal> TopPartyBalances { get; set; } = new();

        public List<string> TopTransporterNames { get; set; } = new();
        public List<decimal> TopTransporterBalances { get; set; } = new();

        // 4. Activity Feeds
        public List<SimpleTransaction> RecentTransactions { get; set; } = new();
        public List<RecentBiltyItem> RecentBilties { get; set; } = new();
        public List<RecentChallanItem> RecentChallans { get; set; } = new();
    }

    public class DashboardCompanyItem
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class DashboardFyItem
    {
        public int Id { get; set; }
        public string YearName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsClosed { get; set; }
    }

    public class SimpleTransaction
    {
        public DateTime Date { get; set; }
        public string DocNo { get; set; } = string.Empty;
        public string Account { get; set; } = string.Empty;
        public string VoType { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Narration { get; set; } = string.Empty;
    }

    public class RecentBiltyItem
    {
        public int Id { get; set; }
        public string DocNo { get; set; } = string.Empty;
        public DateTime DocDate { get; set; }
        public string PartyName { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal Freight { get; set; }
        public string PType { get; set; } = string.Empty; // Paid, ToPay
    }

    public class RecentChallanItem
    {
        public int Id { get; set; }
        public string DocNo { get; set; } = string.Empty;
        public DateTime DocDate { get; set; }
        public string VehicleNo { get; set; } = string.Empty;
        public string Driver { get; set; } = string.Empty;
        public string Transporter { get; set; } = string.Empty;
        public string Station { get; set; } = string.Empty;
        public decimal NetAmt { get; set; }
    }
}
