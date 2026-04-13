using System.Security.Claims;

namespace Nskg.Extensions
{
    public static class ClaimExtensions
    {
        public static int GetCompanyId(this ClaimsPrincipal user)
        {
            return int.Parse(user.FindFirst("CompanyId")?.Value ?? "0");
        }

        public static int GetFinancialYearId(this ClaimsPrincipal user)
        {
            return int.Parse(user.FindFirst("FinancialYearId")?.Value ?? "0");
        }
    }
}
