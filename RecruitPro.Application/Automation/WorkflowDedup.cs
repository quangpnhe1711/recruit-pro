using System;

namespace RecruitPro.Application.Automation;

/// <summary>
/// Deterministic dedup-key builder for durable events. The same logical transition always produces the
/// same key so a re-published event is a no-op (the outbox has a unique index on dedup_key). Pure and
/// unit-testable.
/// </summary>
public static class WorkflowDedup
{
    /// <summary>Key for a normal aggregate transition, e.g. "PassedToHeadReview:app:{id}".</summary>
    public static string ForTransition(string eventType, string aggregateShortType, Guid aggregateId)
        => $"{eventType}:{aggregateShortType}:{aggregateId:N}";

    /// <summary>
    /// Windowed key for time-based events (overdue reminders): includes a date bucket so at most one
    /// event fires per aggregate per window, preventing reminder spam.
    /// </summary>
    public static string ForWindow(string eventType, string aggregateShortType, Guid aggregateId, DateTime windowStart)
        => $"{eventType}:{aggregateShortType}:{aggregateId:N}:{windowStart:yyyyMMddHH}";
}
