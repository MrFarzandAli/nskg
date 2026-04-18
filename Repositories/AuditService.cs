using Nskg.Data;
using Nskg.Models;
using Nskg.Repositories.Interfaces;

namespace Nskg.Repositories
{
    public class AuditService : IAuditService
    {
        private readonly IHttpContextAccessor _httpContext;
        private readonly IServiceScopeFactory _scopeFactory;

        public AuditService(IHttpContextAccessor httpContext, IServiceScopeFactory scopeFactory)
        {
            _httpContext = httpContext;
            _scopeFactory = scopeFactory;
        }

        public async Task LogAsync(string action, string table, string recordId, string details,
            int? companyId = null, int? financialYearId = null)
        {
            var user = _httpContext.HttpContext?.User?.Identity?.Name ?? "Anonymous";

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var log = new AuditLog
            {
                UserName = user,
                Action = action,
                TableName = table,
                RecordId = recordId,
                Details = details,
                CompanyId = companyId,
                FinancialYearId = financialYearId,
                CreatedAt = DateTime.Now
            };

            context.AuditLogs.Add(log);
            await context.SaveChangesAsync(); // ✅ SAFE because isolated context
        }
    }
}
