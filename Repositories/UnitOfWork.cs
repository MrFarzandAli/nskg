using Nskg.Data;
using Nskg.Models;
using Nskg.Repositories.Interfaces;

namespace Nskg.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public IGenericRepository<Company> Companies { get; private set; }
        public IGenericRepository<FinancialYear> FinancialYears { get; private set; }

        public IFinancialYearRepository FinancialYearRepository { get; private set; }

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;

            Companies = new GenericRepository<Company>(_context);
            FinancialYears = new GenericRepository<FinancialYear>(_context);

            FinancialYearRepository = new FinancialYearRepository(_context);
        }

        public async Task<int> SaveAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
