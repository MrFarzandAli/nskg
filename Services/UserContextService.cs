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
            return int.Parse(_httpContext.HttpContext.User.FindFirst("CompanyId").Value);
        }

        public int GetFinancialYearId()
        {
            return int.Parse(_httpContext.HttpContext.User.FindFirst("FinancialYearId").Value);
        }
    }
}
