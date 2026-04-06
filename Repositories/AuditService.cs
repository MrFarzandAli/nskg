using Nskg.Data;
using Nskg.Models;
using Nskg.Repositories.Interfaces;

namespace Nskg.Repositories
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContext;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContext)
        {
            _context = context;
            _httpContext = httpContext;
        }

        public async Task LogAsync(string action, string table, string recordId, string details)
        {
            var user = _httpContext.HttpContext?.User?.Identity?.Name ?? "Anonymous";

            var log = new AuditLog
            {
                UserName = user,
                Action = action,
                TableName = table,
                RecordId = recordId,
                Details = details,
                CreatedAt = DateTime.Now
            };

            await _context.AuditLogs.AddAsync(log);
            await _context.SaveChangesAsync();
        }
    }
}
