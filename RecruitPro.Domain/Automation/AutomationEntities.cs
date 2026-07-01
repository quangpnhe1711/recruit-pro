using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Automation;

/// <summary>
/// Durable event outbox row. Business services write one of these (idempotent on <see cref="DedupKey"/>)
/// after an important, already-committed ATS change. The dispatcher drains Pending rows so a server
/// restart never loses committed workflow work.
/// </summary>
public class PublishedDomainEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string DedupKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public WorkflowEventStatus Status { get; set; } = WorkflowEventStatus.Pending;
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A workflow, owning many immutable versions. The active version drives the dispatcher.</summary>
public class WorkflowDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Guid? ActiveVersionId { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<WorkflowDefinitionVersion> Versions { get; set; } = new List<WorkflowDefinitionVersion>();
    public virtual WorkflowDefinitionVersion? ActiveVersion { get; set; }
}

/// <summary>Immutable, versioned trigger + conditions + actions + cutover mode snapshot.</summary>
public class WorkflowDefinitionVersion
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public int VersionNo { get; set; }
    public string TriggerJson { get; set; } = "{}";
    public string ConditionsJson { get; set; } = "[]";
    public string ActionsJson { get; set; } = "[]";
    public WorkflowMode Mode { get; set; } = WorkflowMode.Shadow;
    public bool IsActive { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual WorkflowDefinition? WorkflowDefinition { get; set; }
}

/// <summary>One run of one workflow version against one event.</summary>
public class WorkflowExecution
{
    public Guid Id { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid WorkflowDefinitionVersionId { get; set; }
    public Guid? EventId { get; set; }
    public string? EventDedupKey { get; set; }
    public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.Pending;
    public WorkflowMode Mode { get; set; }
    public string TriggerEventType { get; set; } = string.Empty;
    public string InputPayloadJson { get; set; } = "{}";
    public string? OutputJson { get; set; }
    public string? ErrorReason { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual WorkflowDefinition? WorkflowDefinition { get; set; }
    public virtual ICollection<WorkflowExecutionStep> Steps { get; set; } = new List<WorkflowExecutionStep>();
}

/// <summary>A single condition-evaluation or action step inside an execution.</summary>
public class WorkflowExecutionStep
{
    public Guid Id { get; set; }
    public Guid ExecutionId { get; set; }
    public int StepNo { get; set; }
    public string StepType { get; set; } = string.Empty;
    public string? ActionType { get; set; }
    public WorkflowStepStatus Status { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public string? ErrorReason { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual WorkflowExecution? Execution { get; set; }
}

/// <summary>A failed action that exhausted retries; kept for inspection and manual retry.</summary>
public class WorkflowActionDeadLetter
{
    public Guid Id { get; set; }
    public Guid ExecutionId { get; set; }
    public Guid? StepId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string ErrorReason { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// One row per background worker (e.g. "dispatcher"), refreshed each loop. A stale/absent beat is how the
/// diagnostics screen proves the automation worker is dead — the single most common cause of "no execution".
/// </summary>
public class WorkerHeartbeat
{
    public string WorkerName { get; set; } = string.Empty;
    public DateTime LastBeatAt { get; set; }
    public string Status { get; set; } = "Running";
    public string? Detail { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Audit row for every internal MCP-style tool call (allowed and denied).</summary>
public class McpToolAudit
{
    public Guid Id { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public Guid? CallerUserId { get; set; }
    public string InputJson { get; set; } = "{}";
    public string? OutputSummaryJson { get; set; }
    public bool Allowed { get; set; }
    public string? DeniedReason { get; set; }
    public int? LatencyMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
