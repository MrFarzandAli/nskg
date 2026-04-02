using Nskg.Models;

namespace Nskg.Repositories.Interfaces
{
    public interface IFinancialYearRepository
    {
        Task<IEnumerable<FinancialYear>> GetAllWithCompanyAsync();
        Task<IEnumerable<FinancialYear>> GetByCompanyAsync(int companyId);
    }
}
