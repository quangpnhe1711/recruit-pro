using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Common;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Services.Automation;

/// <summary>
/// Durable event publisher + cutover resolver. Writing an event is idempotent (unique dedup key) and
/// best-effort: a failure here never throws back into the committed ATS action. Returns the effective
/// mode so the caller knows whether to skip its old direct notification (Live) or keep it (Shadow/Disabled).
/// </summary>
public class RecruitProEventBus : IRecruitProEventBus
{
    private readonly IEventOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly WorkflowAutomationSettings _settings;
    private readonly ILogger<RecruitProEventBus> _logger;

    public RecruitProEventBus(
        IEventOutboxRepository outbox,
        IUnitOfWork unitOfWork,
        IOptions<WorkflowAutomationSettings> settings,
        ILogger<RecruitProEventBus> logger)
    {
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<WorkflowMode> PublishAsync(
        string eventType, string aggregateType, Guid aggregateId, string dedupKey, object payload, DateTime occurredAt)
    {
        WorkflowMode mode = _settings.ResolveMode(eventType);
        if (mode == WorkflowMode.Disabled)
        {
            return WorkflowMode.Disabled;
        }

        try
        {
            if (await _outbox.ExistsByDedupKeyAsync(dedupKey))
            {
                return mode; // Already published for this transition — idempotent no-op.
            }

            await _outbox.AddAsync(new PublishedDomainEvent
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                AggregateType = aggregateType,
                AggregateId = aggregateId,
                DedupKey = dedupKey,
                PayloadJson = JsonSerializer.Serialize(payload),
                Status = WorkflowEventStatus.Pending,
                OccurredAt = DateTime.SpecifyKind(occurredAt, DateTimeKind.Unspecified),
                AttemptCount = 0,
                CreatedAt = DbDateTime.Now,
            });
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Never break the committed business action; a lost event is recoverable, a broken ATS write is not.
            _logger.LogError(ex, "Failed to publish domain event {EventType} for {AggregateId}", eventType, aggregateId);
        }

        return mode;
    }
}
