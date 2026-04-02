using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Models;
using Nskg.Repositories.Interfaces;

namespace Nskg.Repositories
{
   

    public class FinancialYearRepository : IFinancialYearRepository
    {
        private readonly ApplicationDbContext _context;

        public FinancialYearRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<FinancialYear>> GetAllWithCompanyAsync()
        {
            return await _context.FinancialYears
                .Include(x => x.Company)
                .ToListAsync();
        }

        public async Task<IEnumerable<FinancialYear>> GetByCompanyAsync(int companyId)
        {
            return await _context.FinancialYears
                .Where(x => x.CompanyId == companyId)
                .Include(x => x.Company)
                .ToListAsync();
        }
    }
}
