using System.Security.Claims;

namespace Nskg.Extensions
{
    public static class ClaimExtensions
    {
        public static int GetCompanyId(this ClaimsPrincipal user)
        {
            return int.Parse(user.FindFirst("CompanyId")?.Value ?? "0");
        }
        public static string GetCompanyCode(this ClaimsPrincipal user)
        {
            return user.FindFirst("CompanyCode")?.Value ?? "0";
        }
        public static string GetCompanyName(this ClaimsPrincipal user)
        {
            return user.FindFirst("CompanyName")?.Value ?? "";
        }

        public static int GetFinancialYearId(this ClaimsPrincipal user)
        {
            return int.Parse(user.FindFirst("FinancialYearId")?.Value ?? "0");
        }
        public static string GetUserId(this ClaimsPrincipal user)
        {
            var value = user.FindFirstValue(ClaimTypes.NameIdentifier);

            return value; 
        }
    }
}
