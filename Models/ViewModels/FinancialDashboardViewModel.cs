using System;
using System.Collections.Generic;

namespace Nskg.Models.ViewModels
{
    public class FinancialDashboardViewModel
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalReceivables { get; set; }
        public decimal CashBalance { get; set; }

        public List<decimal> MonthlyRevenue { get; set; } = new();
        public List<string> MonthlyLabels { get; set; } = new();

        public List<SimpleTransaction> RecentTransactions { get; set; } = new();
    }

    public class SimpleTransaction
    {
        public DateTime Date { get; set; }
        public string Account { get; set; } = null!;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Narration { get; set; } = null!;
    }
}
