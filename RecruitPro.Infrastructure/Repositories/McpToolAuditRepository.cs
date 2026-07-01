using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Automation;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories;

public class McpToolAuditRepository : IMcpToolAuditRepository
{
    private readonly AppDbContext _context;

    public McpToolAuditRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(McpToolAudit audit)
        => await _context.McpToolAudits.AddAsync(audit);

    public Task<McpToolAudit?> GetByIdAsync(Guid id)
        => _context.McpToolAudits.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);

    public async Task<(IReadOnlyList<McpToolAudit> Items, int Total)> QueryAsync(
        string? toolName, bool? allowed, int page, int pageSize)
    {
        IQueryable<McpToolAudit> query = _context.McpToolAudits.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(toolName))
        {
            query = query.Where(a => a.ToolName == toolName);
        }
        if (allowed.HasValue)
        {
            query = query.Where(a => a.Allowed == allowed.Value);
        }

        int total = await query.CountAsync();
        List<McpToolAudit> items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<McpToolAudit?> GetLastForToolAsync(string toolName)
        => _context.McpToolAudits.AsNoTracking()
            .Where(a => a.ToolName == toolName)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();
}
