using System.Collections.Generic;
using System.Text.Json;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Automation;

/// <summary>Structured trigger: which durable event type fires this workflow.</summary>
public sealed class WorkflowTriggerModel
{
    public string EventType { get; set; } = string.Empty;
}

/// <summary>One structured condition, evaluated against a flat field of the event payload.</summary>
public sealed class WorkflowConditionModel
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    /// <summary>Comparison value as a raw string (numbers/dates/comma-lists parsed by the evaluator).</summary>
    public string? Value { get; set; }
}

/// <summary>One action: a registered handler key plus its opaque JSON config.</summary>
public sealed class WorkflowActionModel
{
    public string Type { get; set; } = string.Empty;
    public JsonElement Config { get; set; }
}

/// <summary>Supported condition operators for the structured builder.</summary>
public static class WorkflowConditionOperator
{
    public const string Equals = "equals";
    public const string NotEquals = "not_equals";
    public const string GreaterThan = "greater_than";
    public const string GreaterThanOrEqual = "greater_than_or_equal";
    public const string Exists = "exists";
    public const string InList = "in_list";
    public const string OlderThanDays = "older_than_days";

    public static readonly string[] All =
    [
        Equals, NotEquals, GreaterThan, GreaterThanOrEqual, Exists, InList, OlderThanDays
    ];
}

/// <summary>Outcome of evaluating a workflow's whole condition set (AND semantics).</summary>
public sealed class ConditionEvaluationResult
{
    public bool Passed { get; init; }
    public string? FailedField { get; init; }
    public string? Detail { get; init; }

    public static ConditionEvaluationResult Pass() => new() { Passed = true };
    public static ConditionEvaluationResult Fail(string field, string detail)
        => new() { Passed = false, FailedField = field, Detail = detail };
}

/// <summary>Everything an action handler needs. Mode drives shadow (record-only) vs live (send).</summary>
public sealed class WorkflowActionContext
{
    public required WorkflowMode Mode { get; init; }
    public required string EventType { get; init; }
    public required JsonElement Payload { get; init; }
    public required JsonElement Config { get; init; }
    public required Guid ExecutionId { get; init; }
}

/// <summary>Result of running one action step.</summary>
public sealed class WorkflowActionResult
{
    public WorkflowStepStatus Status { get; init; }
    public object? Output { get; init; }
    public string? Error { get; init; }

    public static WorkflowActionResult Ok(object? output = null)
        => new() { Status = WorkflowStepStatus.Success, Output = output };
    public static WorkflowActionResult Skip(object? output = null)
        => new() { Status = WorkflowStepStatus.Skipped, Output = output };
    public static WorkflowActionResult Fail(string error, object? output = null)
        => new() { Status = WorkflowStepStatus.Failed, Output = output, Error = error };
}
