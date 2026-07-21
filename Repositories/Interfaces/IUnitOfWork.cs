using Nskg.Models;


namespace Nskg.Repositories.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<Company> Companies { get; }
        IGenericRepository<FinancialYear> FinancialYears { get; }
        IGenericRepository<Actype> Actype { get; }
        IGenericRepository<AccCat> AccCat { get; }


        IFinancialYearRepository FinancialYearRepository { get; }
        ICompanyRepository companyRepository{ get; }


        Task<int> SaveAsync();
    }
}
