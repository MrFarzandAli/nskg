using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Models.ViewModels;

namespace Nskg.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Financial()
        {
            var vm = new FinancialDashboardViewModel();

            // Top level aggregates
            vm.TotalRevenue = _context.GLTrans.Sum(t => t.Credit) - _context.GLTrans.Sum(t => t.Debit);
            vm.TotalExpenses = _context.CommHead.Sum(c => (decimal?)c.TotNet) ?? 0m;

            vm.TotalReceivables = _context.GLTrans.Where(t => t.Debit > 0).Sum(t => (decimal?)t.Debit) ?? 0m;
            vm.CashBalance = _context.GLTrans.Where(t => t.Accode == "CASH" ).Sum(t => (decimal?) (t.Debit - t.Credit)) ?? 0m;

            // Monthly revenue for last 6 months
            var now = DateTime.Now;
            for (int i = 5; i >= 0; i--)
            {
                var month = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                vm.MonthlyLabels.Add(month.ToString("MMM yy"));

                var monthSum = _context.GLTrans
                    .Where(t => t.Docdate.Year == month.Year && t.Docdate.Month == month.Month)
                    .Sum(t => (decimal?)(t.Credit - t.Debit)) ?? 0m;

                vm.MonthlyRevenue.Add(monthSum);
            }

            // Recent transactions (top 10)
            vm.RecentTransactions = _context.GLTrans
                .OrderByDescending(t => t.Docdate)
                .Take(10)
                .Select(t => new SimpleTransaction
                {
                    Date = t.Docdate,
                    Account = t.Accode,
                    Debit = t.Debit,
                    Credit = t.Credit,
                    Narration = t.Narration ?? string.Empty
                })
                .ToList();

            return View(vm);
        }
    }
}
