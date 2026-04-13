namespace Nskg.Repositories.Interfaces
{
    public interface IAuditService
    {
        Task LogAsync(string action, string table, string recordId, string details, int? companyId = null,        // new optional
            int? financialYearId = null  );
    }
}
