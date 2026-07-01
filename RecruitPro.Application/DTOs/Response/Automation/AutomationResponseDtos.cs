using System;
using System.Collections.Generic;

namespace RecruitPro.Application.DTOs.Response.Automation;

public class WorkflowConditionDto
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public class WorkflowActionDto
{
    public string Type { get; set; } = string.Empty;
    /// <summary>Raw config JSON (shown inside an expandable section on the UI, never as the only content).</summary>
    public string ConfigJson { get; set; } = "{}";
    public string? Description { get; set; }
}

public class WorkflowVersionDto
{
    public string Id { get; set; } = string.Empty;
    public int VersionNo { get; set; }
    public string TriggerEventType { get; set; } = string.Empty;
    public List<WorkflowConditionDto> Conditions { get; set; } = new();
    public List<WorkflowActionDto> Actions { get; set; } = new();
    public string Mode { get; set; } = "Shadow";
    public bool IsActive { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WorkflowSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int? ActiveVersionNo { get; set; }
    public string? TriggerEventType { get; set; }
    public string Mode { get; set; } = "Disabled";
    public DateTime? LastRunAt { get; set; }
    public string? LastStatus { get; set; }
}

public class WorkflowDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public WorkflowVersionDto? ActiveVersion { get; set; }
    public List<WorkflowVersionDto> Versions { get; set; } = new();
    public List<ExecutionSummaryDto> RecentExecutions { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class WorkflowStepDto
{
    public string Id { get; set; } = string.Empty;
    public int StepNo { get; set; }
    public string StepType { get; set; } = string.Empty;
    public string? ActionType { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public string? ErrorReason { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public class ExecutionSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string WorkflowDefinitionId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? DurationMs { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorReason { get; set; }
    public bool RetryAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExecutionDetailDto : ExecutionSummaryDto
{
    public string InputPayloadJson { get; set; } = "{}";
    public string? OutputJson { get; set; }
    public WorkflowVersionDto? VersionSnapshot { get; set; }
    public List<WorkflowStepDto> Steps { get; set; } = new();
}

public class OutboxEventDto
{
    public string Id { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string DedupKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorReason { get; set; }
    public string PayloadJson { get; set; } = "{}";
}

public class AutomationDashboardDto
{
    public int TotalWorkflows { get; set; }
    public int EnabledWorkflows { get; set; }
    public int ExecutionsToday { get; set; }
    public int FailedExecutions { get; set; }
    public int DeadLetterCount { get; set; }
    public string? MostCommonFailedAction { get; set; }
    public List<ExecutionSummaryDto> RecentExecutions { get; set; } = new();
}

public class WorkerHeartbeatDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime? LastBeatAt { get; set; }
    public double? SecondsSinceBeat { get; set; }
    public bool IsStale { get; set; }
    public string Status { get; set; } = "Unknown";
    public string? Detail { get; set; }
}

public class DiagnosticsEventDto
{
    public string EventType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

public class DiagnosticsExecutionDto
{
    public string WorkflowName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Answers "is automation alive and why did / didn't it run?" without reading raw JSON.</summary>
public class AutomationDiagnosticsDto
{
    public bool AutomationEnabled { get; set; }
    public string DefaultMode { get; set; } = "Shadow";
    public List<WorkerHeartbeatDto> Workers { get; set; } = new();
    public bool DispatcherHealthy { get; set; }
    public int PendingEvents { get; set; }
    public int ProcessingEvents { get; set; }
    public int FailedEvents { get; set; }
    public int DeadLetterEvents { get; set; }
    public int ExecutionsToday { get; set; }
    public int FailedExecutions { get; set; }
    public int UnresolvedDeadLetters { get; set; }
    public DiagnosticsEventDto? LatestEvent { get; set; }
    public DiagnosticsExecutionDto? LatestExecution { get; set; }
    public List<string> Warnings { get; set; } = new();
}

/// <summary>Per-workflow diagnostics: whether it can run and, if not, the human reason why.</summary>
public class WorkflowDiagnosticsDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool HasActiveVersion { get; set; }
    public string? VersionMode { get; set; }
    public string EffectiveMode { get; set; } = "Disabled";
    public string? TriggerEventType { get; set; }
    public int EventsTodayOfType { get; set; }
    public int PendingEventsOfType { get; set; }
    public int ExecutionsToday { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public DiagnosticsEventDto? LatestMatchingEvent { get; set; }
    public DiagnosticsExecutionDto? LatestExecution { get; set; }
    /// <summary>Vietnamese reason no execution appears; null means the workflow is running normally.</summary>
    public string? NoExecutionReason { get; set; }
    public bool Healthy => NoExecutionReason == null;
}

public class McpToolDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> PermissionsRequired { get; set; } = new();
    public string Access { get; set; } = "read";
    public bool Enabled { get; set; } = true;
    public DateTime? LastCalledAt { get; set; }
}

public class McpAuditDto
{
    public string Id { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string? CallerUserId { get; set; }
    public bool Allowed { get; set; }
    public string? DeniedReason { get; set; }
    public int? LatencyMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public string InputJson { get; set; } = "{}";
    public string? OutputSummaryJson { get; set; }
}

/// <summary>Result of an internal MCP tool call (used by the test endpoint + workflow AI actions later).</summary>
public class McpToolResult
{
    public bool Allowed { get; set; }
    public string? DeniedReason { get; set; }
    public object? Output { get; set; }
}
