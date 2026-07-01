using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Automation;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories;

public class EventOutboxRepository : IEventOutboxRepository
{
    private readonly AppDbContext _context;

    public EventOutboxRepository(AppDbContext context) => _context = context;

    public Task<bool> ExistsByDedupKeyAsync(string dedupKey)
        => _context.PublishedDomainEvents.AsNoTracking().AnyAsync(e => e.DedupKey == dedupKey);

    public async Task AddAsync(PublishedDomainEvent domainEvent)
        => await _context.PublishedDomainEvents.AddAsync(domainEvent);

    public Task<PublishedDomainEvent?> GetTrackedByIdAsync(Guid id)
        => _context.PublishedDomainEvents.FirstOrDefaultAsync(e => e.Id == id);

    public Task<PublishedDomainEvent?> GetByIdAsync(Guid id)
        => _context.PublishedDomainEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);

    public async Task<IReadOnlyList<PublishedDomainEvent>> GetDispatchableAsync(DateTime now, int max)
    {
        return await _context.PublishedDomainEvents
            .Where(e => e.Status == WorkflowEventStatus.Pending
                        || (e.Status == WorkflowEventStatus.Failed && e.NextAttemptAt != null && e.NextAttemptAt <= now))
            .OrderBy(e => e.OccurredAt)
            .Take(max)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<PublishedDomainEvent> Items, int Total)> QueryAsync(
        string? status, string? eventType, DateTime? from, DateTime? to, int page, int pageSize)
    {
        IQueryable<PublishedDomainEvent> query = _context.PublishedDomainEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse(status, true, out WorkflowEventStatus s))
        {
            query = query.Where(e => e.Status == s);
        }
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }
        if (from.HasValue)
        {
            query = query.Where(e => e.OccurredAt >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(e => e.OccurredAt <= to.Value);
        }

        int total = await query.CountAsync();
        List<PublishedDomainEvent> items = await query
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
