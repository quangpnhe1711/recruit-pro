using System.Text.Json;
using RecruitPro.Application.Automation;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Interfaces.IServices.Automation;

/// <summary>
/// Writes a durable domain event to the outbox (idempotent on dedup key) and returns the effective
/// cutover mode for the caller. In Live mode the caller must SKIP its old direct notification so exactly
/// one source sends. In Shadow/Disabled the caller proceeds with its direct notification as before.
/// </summary>
public interface IRecruitProEventBus
{
    Task<WorkflowMode> PublishAsync(
        string eventType,
        string aggregateType,
        Guid aggregateId,
        string dedupKey,
        object payload,
        DateTime occurredAt);
}

public interface IWorkflowConditionEvaluator
{
    ConditionEvaluationResult Evaluate(
        IReadOnlyList<WorkflowConditionModel> conditions, JsonElement payload, DateTime now);
}

public interface IWorkflowActionHandler
{
    string ActionType { get; }
    Task<WorkflowActionResult> ExecuteAsync(WorkflowActionContext context, CancellationToken cancellationToken = default);
}

public interface IWorkflowActionRegistry
{
    IWorkflowActionHandler? Resolve(string actionType);
    IReadOnlyCollection<string> RegisteredTypes { get; }
}

/// <summary>Processes exactly one pending outbox event: match -> evaluate -> act -> log. Idempotent.</summary>
public interface IWorkflowEngine
{
    Task ProcessEventAsync(Guid eventId, CancellationToken cancellationToken = default);
}

public interface IWorkflowRetryService
{
    /// <summary>Re-runs a failed/dead-letter execution's actions; returns the resulting status.</summary>
    Task<WorkflowExecutionStatus> RetryExecutionAsync(Guid executionId, bool manual, CancellationToken cancellationToken = default);
    Task ProcessDueRetriesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates + persists + realtime-pushes notifications for workflow actions. In Shadow mode it resolves
/// recipients but sends nothing (returns the would-notify recipient ids).
/// </summary>
public interface IWorkflowNotificationDispatcher
{
    Task<IReadOnlyList<Guid>> DispatchAsync(
        IReadOnlyCollection<Guid> userIds,
        string eventCode,
        string title,
        string body,
        string? dataJson,
        WorkflowMode mode,
        CancellationToken cancellationToken = default);
}

/// <summary>Seeds the four default workflow templates (idempotent by name).</summary>
public interface IWorkflowTemplateSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

/// <summary>Scans ManagerReview applications past the overdue threshold and publishes HeadReviewOverdue.</summary>
public interface IHeadReviewOverdueScanner
{
    Task<int> ScanAsync(CancellationToken cancellationToken = default);
}
