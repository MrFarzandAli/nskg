using Microsoft.AspNetCore.Http;
using Nskg.Models;
using System;
using System.Text;
using System.Text.Json;

namespace Nskg.Helper
{
    public static class CompanyCookieHelper
    {
        public const string CookieName = "NSKG_COMPANY_CONTEXT";

        public static void SetCompanyCookie(HttpResponse response, CompanyCookieContext data, bool isHttps)
        {
            try
            {
                var json = JsonSerializer.Serialize(data);
                var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

                response.Cookies.Append(CookieName, base64, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = isHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddDays(30)
                });
            }
            catch
            {
                // Ignore any serialization failure
            }
        }

        public static CompanyCookieContext? GetCompanyCookie(HttpRequest request)
        {
            if (!request.Cookies.TryGetValue(CookieName, out var base64) || string.IsNullOrWhiteSpace(base64))
                return null;

            try
            {
                var bytes = Convert.FromBase64String(base64);
                var json = Encoding.UTF8.GetString(bytes);
                return JsonSerializer.Deserialize<CompanyCookieContext>(json);
            }
            catch
            {
                return null;
            }
        }

        public static void ClearCompanyCookie(HttpResponse response)
        {
            response.Cookies.Delete(CookieName);
        }
    }
}
