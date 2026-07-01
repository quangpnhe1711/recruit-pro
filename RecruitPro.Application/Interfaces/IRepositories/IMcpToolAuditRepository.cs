using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Interfaces.IRepositories;

public interface IMcpToolAuditRepository
{
    Task AddAsync(McpToolAudit audit);
    Task<McpToolAudit?> GetByIdAsync(Guid id);
    Task<(IReadOnlyList<McpToolAudit> Items, int Total)> QueryAsync(
        string? toolName, bool? allowed, int page, int pageSize);
    Task<McpToolAudit?> GetLastForToolAsync(string toolName);
}
