using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Automation;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories;

public class WorkflowRepository : IWorkflowRepository
{
    private readonly AppDbContext _context;

    public WorkflowRepository(AppDbContext context) => _context = context;

    // --- definitions / versions ---

    public async Task AddDefinitionAsync(WorkflowDefinition definition)
        => await _context.WorkflowDefinitions.AddAsync(definition);

    public async Task AddVersionAsync(WorkflowDefinitionVersion version)
        => await _context.WorkflowDefinitionVersions.AddAsync(version);

    public Task<WorkflowDefinition?> GetDefinitionAsync(Guid id)
        => _context.WorkflowDefinitions.AsNoTracking()
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id);

    public Task<WorkflowDefinition?> GetDefinitionTrackedAsync(Guid id)
        => _context.WorkflowDefinitions
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id);

    public async Task<IReadOnlyList<WorkflowDefinition>> ListDefinitionsAsync()
        => await _context.WorkflowDefinitions.AsNoTracking()
            .Include(d => d.Versions)
            .OrderBy(d => d.Name)
            .ToListAsync();

    public Task<WorkflowDefinitionVersion?> GetVersionAsync(Guid id)
        => _context.WorkflowDefinitionVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);

    public Task<WorkflowDefinitionVersion?> GetVersionTrackedAsync(Guid id)
        => _context.WorkflowDefinitionVersions.FirstOrDefaultAsync(v => v.Id == id);

    public async Task<int> GetMaxVersionNoAsync(Guid definitionId)
    {
        return await _context.WorkflowDefinitionVersions
            .Where(v => v.WorkflowDefinitionId == definitionId)
            .Select(v => (int?)v.VersionNo)
            .MaxAsync() ?? 0;
    }

    public async Task<IReadOnlyList<WorkflowDefinitionVersion>> GetVersionsAsync(Guid definitionId)
        => await _context.WorkflowDefinitionVersions.AsNoTracking()
            .Where(v => v.WorkflowDefinitionId == definitionId)
            .OrderByDescending(v => v.VersionNo)
            .ToListAsync();

    public async Task<IReadOnlyList<WorkflowDefinitionVersion>> GetActiveEnabledVersionsAsync()
    {
        return await _context.WorkflowDefinitionVersions.AsNoTracking()
            .Include(v => v.WorkflowDefinition)
            .Where(v => v.IsActive && v.WorkflowDefinition!.IsEnabled)
            .ToListAsync();
    }

    public Task<bool> DefinitionExistsByNameAsync(string name)
        => _context.WorkflowDefinitions.AsNoTracking().AnyAsync(d => d.Name == name);

    // --- executions / steps / dead-letters ---

    public async Task AddExecutionAsync(WorkflowExecution execution)
        => await _context.WorkflowExecutions.AddAsync(execution);

    public async Task AddStepAsync(WorkflowExecutionStep step)
        => await _context.WorkflowExecutionSteps.AddAsync(step);

    public async Task AddDeadLetterAsync(WorkflowActionDeadLetter deadLetter)
        => await _context.WorkflowActionDeadLetters.AddAsync(deadLetter);

    public Task<bool> ExecutionExistsAsync(Guid versionId, string dedupKey)
        => _context.WorkflowExecutions.AsNoTracking()
            .AnyAsync(e => e.WorkflowDefinitionVersionId == versionId && e.EventDedupKey == dedupKey);

    public Task<WorkflowExecution?> GetExecutionAsync(Guid id)
        => _context.WorkflowExecutions.AsNoTracking()
            .Include(e => e.WorkflowDefinition)
            .Include(e => e.Steps)
            .FirstOrDefaultAsync(e => e.Id == id);

    public Task<WorkflowExecution?> GetExecutionTrackedAsync(Guid id)
        => _context.WorkflowExecutions
            .Include(e => e.Steps)
            .FirstOrDefaultAsync(e => e.Id == id);

    public async Task<(IReadOnlyList<WorkflowExecution> Items, int Total)> QueryExecutionsAsync(
        Guid? workflowDefinitionId, string? status, string? eventType, string? mode,
        DateTime? from, DateTime? to, int page, int pageSize)
    {
        IQueryable<WorkflowExecution> query = _context.WorkflowExecutions.AsNoTracking()
            .Include(e => e.WorkflowDefinition);

        if (workflowDefinitionId.HasValue)
        {
            query = query.Where(e => e.WorkflowDefinitionId == workflowDefinitionId.Value);
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse(status, true, out WorkflowExecutionStatus s))
        {
            query = query.Where(e => e.Status == s);
        }
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.TriggerEventType == eventType);
        }
        if (!string.IsNullOrWhiteSpace(mode) && Enum.TryParse(mode, true, out WorkflowMode m))
        {
            query = query.Where(e => e.Mode == m);
        }
        if (from.HasValue)
        {
            query = query.Where(e => e.CreatedAt >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(e => e.CreatedAt <= to.Value);
        }

        int total = await query.CountAsync();
        List<WorkflowExecution> items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<IReadOnlyList<WorkflowExecution>> GetRetryableExecutionsAsync(DateTime now, int max)
    {
        return await _context.WorkflowExecutions
            .Where(e => e.Status == WorkflowExecutionStatus.Failed && e.NextRetryAt != null && e.NextRetryAt <= now)
            .OrderBy(e => e.NextRetryAt)
            .Take(max)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<WorkflowExecution>> GetRecentExecutionsAsync(int max)
        => await _context.WorkflowExecutions.AsNoTracking()
            .Include(e => e.WorkflowDefinition)
            .OrderByDescending(e => e.CreatedAt)
            .Take(max)
            .ToListAsync();

    // --- dashboard aggregates ---

    public Task<int> CountExecutionsSinceAsync(DateTime since)
        => _context.WorkflowExecutions.AsNoTracking().CountAsync(e => e.CreatedAt >= since);

    public Task<int> CountExecutionsByStatusAsync(WorkflowExecutionStatus status)
        => _context.WorkflowExecutions.AsNoTracking().CountAsync(e => e.Status == status);

    public Task<int> CountUnresolvedDeadLettersAsync()
        => _context.WorkflowActionDeadLetters.AsNoTracking().CountAsync(d => d.ResolvedAt == null);

    // --- diagnostics ---

    public Task<int> CountExecutionsSinceForWorkflowAsync(Guid definitionId, DateTime since)
        => _context.WorkflowExecutions.AsNoTracking()
            .CountAsync(e => e.WorkflowDefinitionId == definitionId && e.CreatedAt >= since);

    public Task<int> CountExecutionsByStatusSinceForWorkflowAsync(Guid definitionId, WorkflowExecutionStatus status, DateTime since)
        => _context.WorkflowExecutions.AsNoTracking()
            .CountAsync(e => e.WorkflowDefinitionId == definitionId && e.Status == status && e.CreatedAt >= since);

    public Task<WorkflowExecution?> GetLatestExecutionAsync()
        => _context.WorkflowExecutions.AsNoTracking()
            .Include(e => e.WorkflowDefinition)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();

    public Task<WorkflowExecution?> GetLatestExecutionForWorkflowAsync(Guid definitionId)
        => _context.WorkflowExecutions.AsNoTracking()
            .Include(e => e.WorkflowDefinition)
            .Where(e => e.WorkflowDefinitionId == definitionId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();

    // --- worker heartbeat ---

    public async Task UpsertHeartbeatAsync(string workerName, DateTime now, string status, string? detail)
    {
        WorkerHeartbeat? existing = await _context.WorkerHeartbeats.FirstOrDefaultAsync(h => h.WorkerName == workerName);
        if (existing is null)
        {
            await _context.WorkerHeartbeats.AddAsync(new WorkerHeartbeat
            {
                WorkerName = workerName,
                LastBeatAt = now,
                Status = status,
                Detail = detail,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.LastBeatAt = now;
            existing.Status = status;
            existing.Detail = detail;
            existing.UpdatedAt = now;
        }
    }

    public async Task<IReadOnlyList<WorkerHeartbeat>> GetHeartbeatsAsync()
        => await _context.WorkerHeartbeats.AsNoTracking().OrderBy(h => h.WorkerName).ToListAsync();
}
