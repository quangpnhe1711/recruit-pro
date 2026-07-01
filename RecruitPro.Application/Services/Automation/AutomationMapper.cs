using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RecruitPro.Application.Automation;
using RecruitPro.Application.DTOs.Response.Automation;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Services.Automation;

/// <summary>Entity -> DTO projection for the SystemAdmin automation screens.</summary>
public static class AutomationMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static WorkflowVersionDto ToVersionDto(WorkflowDefinitionVersion v)
    {
        WorkflowTriggerModel? trigger = Deserialize<WorkflowTriggerModel>(v.TriggerJson);
        List<WorkflowConditionModel> conditions = Deserialize<List<WorkflowConditionModel>>(v.ConditionsJson) ?? [];
        List<WorkflowActionModel> actions = Deserialize<List<WorkflowActionModel>>(v.ActionsJson) ?? [];

        return new WorkflowVersionDto
        {
            Id = v.Id.ToString(),
            VersionNo = v.VersionNo,
            TriggerEventType = trigger?.EventType ?? string.Empty,
            Conditions = conditions.Select(c => new WorkflowConditionDto
            {
                Field = c.Field,
                Operator = c.Operator,
                Value = c.Value
            }).ToList(),
            Actions = actions.Select(a => new WorkflowActionDto
            {
                Type = a.Type,
                ConfigJson = a.Config.ValueKind == JsonValueKind.Undefined ? "{}" : a.Config.GetRawText(),
                Description = DescribeAction(a.Type)
            }).ToList(),
            Mode = v.Mode.ToString(),
            IsActive = v.IsActive,
            PublishedAt = v.PublishedAt,
            CreatedAt = v.CreatedAt,
        };
    }

    public static ExecutionSummaryDto ToExecutionSummary(WorkflowExecution e)
    {
        int? duration = e.StartedAt.HasValue && e.FinishedAt.HasValue
            ? (int)Math.Max(0, (e.FinishedAt.Value - e.StartedAt.Value).TotalMilliseconds)
            : null;

        return new ExecutionSummaryDto
        {
            Id = e.Id.ToString(),
            WorkflowDefinitionId = e.WorkflowDefinitionId.ToString(),
            WorkflowName = e.WorkflowDefinition?.Name ?? "(workflow)",
            EventType = e.TriggerEventType,
            Mode = e.Mode.ToString(),
            Status = e.Status.ToString(),
            StartedAt = e.StartedAt,
            FinishedAt = e.FinishedAt,
            DurationMs = duration,
            AttemptCount = e.AttemptCount,
            ErrorReason = e.ErrorReason,
            RetryAvailable = e.Status is WorkflowExecutionStatus.Failed or WorkflowExecutionStatus.DeadLetter,
            CreatedAt = e.CreatedAt,
        };
    }

    public static string DescribeAction(string type) => type switch
    {
        WorkflowActionType.NotifyUser => "Gửi thông báo tới người phụ trách",
        WorkflowActionType.NotifyRole => "Gửi thông báo theo vai trò",
        WorkflowActionType.SendReminder => "Gửi nhắc việc (có cooldown)",
        WorkflowActionType.RuleBasedNextStepSuggestion => "Gợi ý bước tiếp theo theo luật",
        WorkflowActionType.ShadowLog => "Ghi log shadow (không gửi)",
        _ => type
    };

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
