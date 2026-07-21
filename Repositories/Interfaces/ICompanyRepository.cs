using Nskg.Models;

namespace Nskg.Repositories.Interfaces
{
    public interface ICompanyRepository
    {
        Task<bool> IsCocodeExist(string Cocode);
    }
}
