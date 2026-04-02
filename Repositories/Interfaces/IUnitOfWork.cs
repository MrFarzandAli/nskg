using Nskg.Models;

namespace Nskg.Repositories.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<Company> Companies { get; }
        IGenericRepository<FinancialYear> FinancialYears { get; }

        IFinancialYearRepository FinancialYearRepository { get; }

        Task<int> SaveAsync();
    }
}
