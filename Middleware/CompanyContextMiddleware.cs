using Microsoft.AspNetCore.Http;
using Nskg.Helper;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Nskg.Middleware
{
    public class CompanyContextMiddleware
    {
        private readonly RequestDelegate _next;

        public CompanyContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // If user is not logged in, let authentication/authorization middleware handle it
            if (context.User.Identity == null || !context.User.Identity.IsAuthenticated)
            {
                await _next(context);
                return;
            }

            // Read browser-specific company cookie
            var cookieData = CompanyCookieHelper.GetCompanyCookie(context.Request);

            if (cookieData != null && cookieData.CompanyId > 0)
            {
                // Remove any stale company claims from all identities
                foreach (var identity in context.User.Identities)
                {
                    var staleClaims = identity.Claims
                        .Where(c => c.Type is "CompanyId" or "CompanyCode" or "CompanyName" or "FinancialYearId")
                        .ToList();

                    foreach (var sc in staleClaims)
                    {
                        identity.TryRemoveClaim(sc);
                    }
                }

                // Add active company claims strictly from this browser's cookie
                var primaryIdentity = context.User.Identities.FirstOrDefault(i => i.IsAuthenticated)
                                      ?? context.User.Identities.FirstOrDefault();

                if (primaryIdentity != null)
                {
                    primaryIdentity.AddClaim(new Claim("CompanyId", cookieData.CompanyId.ToString()));
                    primaryIdentity.AddClaim(new Claim("CompanyCode", cookieData.CompanyCode ?? ""));
                    primaryIdentity.AddClaim(new Claim("CompanyName", cookieData.CompanyName ?? ""));
                    primaryIdentity.AddClaim(new Claim("FinancialYearId", cookieData.FinancialYearId.ToString()));
                }

                await _next(context);
                return;
            }

            // User is authenticated but hasn't selected a company in this browser
            var path = context.Request.Path.Value ?? "";

            // Check if path is an exempt route
            if (IsExemptPath(path, context.Request))
            {
                await _next(context);
                return;
            }

            // Redirect browser to Select Company page
            context.Response.Redirect("/AccountSetup/SelectCompany");
        }

        private static bool IsExemptPath(string path, HttpRequest request)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            // Allow AccountSetup routes (e.g. SelectCompany, GetFinancialYears)
            if (path.StartsWith("/AccountSetup", StringComparison.OrdinalIgnoreCase))
                return true;

            // Allow Identity routes (e.g. Logout, Login)
            if (path.StartsWith("/Identity", StringComparison.OrdinalIgnoreCase))
                return true;

            // Allow static assets
            if (path.StartsWith("/assets", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/plugins", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/images", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/fonts", StringComparison.OrdinalIgnoreCase) ||
                Path.HasExtension(path))
            {
                return true;
            }

            // Allow AJAX / Fetch requests
            if (request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return true;

            return false;
        }
    }
}
