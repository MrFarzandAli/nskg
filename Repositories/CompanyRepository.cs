using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Models;
using Nskg.Repositories.Interfaces;

namespace Nskg.Repositories
{

    public class CompanyRepository : ICompanyRepository
    {
        private readonly ApplicationDbContext _context;

        public CompanyRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsCocodeExist(string cocode)
        {
            return await _context.Companies
                .AnyAsync(x => x.Cocode == cocode);
        }
    }
}
