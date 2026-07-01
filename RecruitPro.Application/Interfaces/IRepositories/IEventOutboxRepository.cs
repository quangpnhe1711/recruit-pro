using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Interfaces.IRepositories;

public interface IEventOutboxRepository
{
    Task<bool> ExistsByDedupKeyAsync(string dedupKey);
    Task AddAsync(PublishedDomainEvent domainEvent);

    /// <summary>Tracked load so the caller can mutate + SaveChanges through the shared UnitOfWork.</summary>
    Task<PublishedDomainEvent?> GetTrackedByIdAsync(Guid id);
    Task<PublishedDomainEvent?> GetByIdAsync(Guid id);

    /// <summary>Pending (or retry-due Failed) events ready to dispatch, oldest first.</summary>
    Task<IReadOnlyList<PublishedDomainEvent>> GetDispatchableAsync(DateTime now, int max);

    Task<(IReadOnlyList<PublishedDomainEvent> Items, int Total)> QueryAsync(
        string? status, string? eventType, DateTime? from, DateTime? to, int page, int pageSize);
}
