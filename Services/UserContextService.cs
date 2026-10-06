using Microsoft.AspNetCore.Http;
using Nskg.Service.Interfaces;
using System.Security.Claims;

namespace Nskg.Service
{   
    public class UserContextService : IUserContextService
    {
        private readonly IHttpContextAccessor _httpContext;

        public UserContextService(IHttpContextAccessor httpContext)
        {
            _httpContext = httpContext;
        }

        public int GetCompanyId()
        {
            var claim = _httpContext.HttpContext?.User?.FindFirst("CompanyId")?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        public int GetFinancialYearId()
        {
            var claim = _httpContext.HttpContext?.User?.FindFirst("FinancialYearId")?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }
    }
}
