namespace Nskg.Repositories.Interfaces
{
    public interface IAuditService
    {
        Task LogAsync(string action, string table, string recordId, string details);
    }
}
