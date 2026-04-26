using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Models;

namespace Nskg.Services
{
    public class FinancialYearClosingService
    {
        private readonly ApplicationDbContext _context;

        public FinancialYearClosingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task CloseFinancialYearAsync(
            int companyId,
            int closingFinancialYearId,
            string retainedEarningsAccode)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                //---------------------------------------
                // 1. Validate FY Exists / Not Closed
                //---------------------------------------
                var fy = await _context.FinancialYears
                    .FirstOrDefaultAsync(x =>
                        x.Id == closingFinancialYearId &&
                        x.CompanyId == companyId);

                if (fy == null)
                    throw new Exception("Financial Year not found.");

                if (fy.IsClosed)
                    throw new Exception("Financial Year already closed.");

                //---------------------------------------
                // 2. Validate Next FY Exists
                //---------------------------------------
                var nextFy = await _context.FinancialYears
                    .Where(x =>
                        x.CompanyId == companyId &&
                        x.StartDate > fy.EndDate)
                    .OrderBy(x => x.StartDate)
                    .FirstOrDefaultAsync();

                if (nextFy == null)
                    throw new Exception("Next Financial Year not configured.");

                //---------------------------------------
                // 3. Get GL Balances of Closing FY
                //---------------------------------------
                var balances = await _context.GLTrans
                    .Where(x =>
                        x.CompanyId == companyId &&
                        x.FinancialYearId == closingFinancialYearId)
                    .GroupBy(x => x.Accode)
                    .Select(g => new
                    {
                        Accode = g.Key,
                        Balance = g.Sum(x => x.Debit - x.Credit)
                    })
                    .ToListAsync();

                //---------------------------------------
                // 4. Calculate Net Profit/Loss
                //---------------------------------------
                decimal netProfit = 0;

                foreach (var bal in balances)
                {
                    var acc = await _context.GLChart3
                        .FirstOrDefaultAsync(x =>
                            x.ACC == bal.Accode &&
                            x.CoCode == companyId.ToString());

                    if (acc == null)
                        continue;

                    if (acc.AcType == "I")
                        netProfit += bal.Balance * -1;

                    if (acc.AcType == "E")
                        netProfit -= bal.Balance;
                }

                //---------------------------------------
                // 5. Remove Existing Opening Balances (Safety)
                //---------------------------------------
                var existingOpening = await _context.OpeningBalances
                    .Where(x =>
                        x.CompanyId == companyId &&
                        x.FinancialYearId == nextFy.Id)
                    .ToListAsync();

                if (existingOpening.Any())
                    _context.OpeningBalances.RemoveRange(existingOpening);

                //---------------------------------------
                // 6. Carry Forward Balance Sheet Accounts
                //---------------------------------------
                foreach (var bal in balances)
                {
                    var acc = await _context.GLChart3
                        .FirstOrDefaultAsync(x =>
                            x.ACC == bal.Accode &&
                            x.CoCode == companyId.ToString());

                    if (acc == null)
                        continue;

                    if (acc.AcType == "I" ||
                        acc.AcType == "E")
                        continue;

                    _context.OpeningBalances.Add(new OpeningBalance
                    {
                        CompanyId = companyId,
                        FinancialYearId = nextFy.Id,
                        Accode = bal.Accode,
                        Debit = bal.Balance > 0 ? bal.Balance : 0,
                        Credit = bal.Balance < 0 ? Math.Abs(bal.Balance) : 0
                    });
                }

                //---------------------------------------
                // 7. Post Net Profit to Retained Earnings
                //---------------------------------------
                if (netProfit != 0)
                {
                    _context.OpeningBalances.Add(new OpeningBalance
                    {
                        CompanyId = companyId,
                        FinancialYearId = nextFy.Id,
                        Accode = retainedEarningsAccode,
                        Debit = netProfit < 0 ? Math.Abs(netProfit) : 0,
                        Credit = netProfit > 0 ? netProfit : 0
                    });
                }

                //---------------------------------------
                // 8. Mark FY Closed
                //---------------------------------------
                fy.IsClosed = true;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}