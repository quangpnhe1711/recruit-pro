using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Automation;
using RecruitPro.Application.Common;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Services.Automation;

/// <summary>
/// Processes one pending outbox event: match active workflows by trigger, evaluate conditions, run
/// actions, and persist execution + step logs. Idempotent (unique (version, dedup) execution index).
/// Effective shadow/live is the per-event cutover mode from settings — the single source of truth that
/// is paired with the direct-notification skip in NotificationEventService, so exactly one source sends.
/// A single action failure fails only its execution (retried separately); it never rolls back ATS state
/// and never blocks other workflows for the same event.
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IEventOutboxRepository _outbox;
    private readonly IWorkflowRepository _workflows;
    private readonly IWorkflowConditionEvaluator _conditionEvaluator;
    private readonly IWorkflowActionRegistry _actionRegistry;
    private readonly IUnitOfWork _unitOfWork;
    private readonly WorkflowAutomationSettings _settings;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IEventOutboxRepository outbox,
        IWorkflowRepository workflows,
        IWorkflowConditionEvaluator conditionEvaluator,
        IWorkflowActionRegistry actionRegistry,
        IUnitOfWork unitOfWork,
        IOptions<WorkflowAutomationSettings> settings,
        ILogger<WorkflowEngine> logger)
    {
        _outbox = outbox;
        _workflows = workflows;
        _conditionEvaluator = conditionEvaluator;
        _actionRegistry = actionRegistry;
        _unitOfWork = unitOfWork;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ProcessEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        PublishedDomainEvent? domainEvent = await _outbox.GetTrackedByIdAsync(eventId);
        if (domainEvent is null || domainEvent.Status == WorkflowEventStatus.Processed)
        {
            return;
        }

        domainEvent.Status = WorkflowEventStatus.Processing;
        await _unitOfWork.SaveChangesAsync();

        try
        {
            using JsonDocument payloadDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(domainEvent.PayloadJson) ? "{}" : domainEvent.PayloadJson);
            JsonElement payload = payloadDoc.RootElement.Clone();

            WorkflowMode effectiveMode = _settings.ResolveMode(domainEvent.EventType);
            IReadOnlyList<WorkflowDefinitionVersion> versions = await _workflows.GetActiveEnabledVersionsAsync();

            foreach (WorkflowDefinitionVersion version in versions)
            {
                if (!TriggerMatches(version, domainEvent.EventType) || version.Mode == WorkflowMode.Disabled)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(domainEvent.DedupKey)
                    && await _workflows.ExecutionExistsAsync(version.Id, domainEvent.DedupKey))
                {
                    continue; // Already ran this workflow for this event.
                }

                await RunWorkflowAsync(version, domainEvent, payload, effectiveMode, cancellationToken);
            }

            domainEvent.Status = WorkflowEventStatus.Processed;
            domainEvent.ProcessedAt = DbDateTime.Now;
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow dispatch failed for event {EventId}", eventId);
            domainEvent.Status = WorkflowEventStatus.Failed;
            domainEvent.AttemptCount += 1;
            domainEvent.ErrorReason = ex.Message;
            domainEvent.NextAttemptAt = NextRetry(domainEvent.AttemptCount);
            if (domainEvent.AttemptCount >= _settings.MaxAttempts)
            {
                domainEvent.Status = WorkflowEventStatus.DeadLetter;
            }
            await _unitOfWork.SaveChangesAsync();
        }
    }

    private async Task RunWorkflowAsync(
        WorkflowDefinitionVersion version,
        PublishedDomainEvent domainEvent,
        JsonElement payload,
        WorkflowMode effectiveMode,
        CancellationToken cancellationToken)
    {
        WorkflowExecution execution = new()
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = version.WorkflowDefinitionId,
            WorkflowDefinitionVersionId = version.Id,
            EventId = domainEvent.Id,
            EventDedupKey = domainEvent.DedupKey,
            Status = WorkflowExecutionStatus.Running,
            Mode = effectiveMode,
            TriggerEventType = domainEvent.EventType,
            InputPayloadJson = domainEvent.PayloadJson,
            AttemptCount = 1,
            StartedAt = DbDateTime.Now,
            CreatedAt = DbDateTime.Now,
        };
        await _workflows.AddExecutionAsync(execution);
        await _unitOfWork.SaveChangesAsync();

        int stepNo = 1;

        // --- conditions ---
        List<WorkflowConditionModel> conditions = Deserialize<List<WorkflowConditionModel>>(version.ConditionsJson) ?? [];
        ConditionEvaluationResult conditionResult = _conditionEvaluator.Evaluate(conditions, payload, DbDateTime.Now);
        await AddStepAsync(execution.Id, stepNo++, WorkflowStepType.ConditionEvaluation, null,
            conditionResult.Passed ? WorkflowStepStatus.Success : WorkflowStepStatus.Skipped,
            version.ConditionsJson,
            Serialize(new { passed = conditionResult.Passed, failedField = conditionResult.FailedField, detail = conditionResult.Detail }),
            conditionResult.Passed ? null : conditionResult.Detail);

        if (!conditionResult.Passed)
        {
            execution.Status = WorkflowExecutionStatus.Skipped;
            execution.OutputJson = Serialize(new { skipped = true, reason = conditionResult.Detail, field = conditionResult.FailedField });
            execution.FinishedAt = DbDateTime.Now;
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        // --- actions ---
        List<WorkflowActionModel> actions = Deserialize<List<WorkflowActionModel>>(version.ActionsJson) ?? [];
        List<object?> outputs = [];
        foreach (WorkflowActionModel action in actions)
        {
            IWorkflowActionHandler? handler = _actionRegistry.Resolve(action.Type);
            if (handler is null)
            {
                await AddStepAsync(execution.Id, stepNo++, WorkflowStepType.Action, action.Type,
                    WorkflowStepStatus.Failed, Serialize(action), null, $"Unknown action handler '{action.Type}'");
                await FailExecutionAsync(execution, action.Type, domainEvent.PayloadJson, $"Unknown action handler '{action.Type}'", null);
                return;
            }

            WorkflowActionContext context = new()
            {
                Mode = effectiveMode,
                EventType = domainEvent.EventType,
                Payload = payload,
                Config = action.Config,
                ExecutionId = execution.Id,
            };

            WorkflowActionResult result;
            try
            {
                result = await handler.ExecuteAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                result = WorkflowActionResult.Fail(ex.Message);
            }

            WorkflowExecutionStep step = await AddStepAsync(execution.Id, stepNo++, WorkflowStepType.Action, action.Type,
                result.Status, Serialize(action), Serialize(result.Output), result.Error);
            outputs.Add(result.Output);

            if (result.Status == WorkflowStepStatus.Failed)
            {
                await FailExecutionAsync(execution, action.Type, domainEvent.PayloadJson, result.Error ?? "action failed", step.Id);
                return;
            }
        }

        execution.Status = WorkflowExecutionStatus.Success;
        execution.OutputJson = Serialize(new { mode = effectiveMode.ToString(), steps = outputs });
        execution.FinishedAt = DbDateTime.Now;
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task FailExecutionAsync(WorkflowExecution execution, string actionType, string payloadJson, string error, Guid? stepId)
    {
        execution.Status = WorkflowExecutionStatus.Failed;
        execution.ErrorReason = error;
        execution.FinishedAt = DbDateTime.Now;
        execution.NextRetryAt = NextRetry(execution.AttemptCount);

        await _workflows.AddDeadLetterAsync(new WorkflowActionDeadLetter
        {
            Id = Guid.NewGuid(),
            ExecutionId = execution.Id,
            StepId = stepId,
            ActionType = actionType,
            PayloadJson = payloadJson,
            ErrorReason = error,
            AttemptCount = execution.AttemptCount,
            NextRetryAt = execution.NextRetryAt,
            CreatedAt = DbDateTime.Now,
        });
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<WorkflowExecutionStep> AddStepAsync(
        Guid executionId, int stepNo, string stepType, string? actionType,
        WorkflowStepStatus status, string? input, string? output, string? error)
    {
        WorkflowExecutionStep step = new()
        {
            Id = Guid.NewGuid(),
            ExecutionId = executionId,
            StepNo = stepNo,
            StepType = stepType,
            ActionType = actionType,
            Status = status,
            InputJson = input,
            OutputJson = output,
            ErrorReason = error,
            StartedAt = DbDateTime.Now,
            FinishedAt = DbDateTime.Now,
            CreatedAt = DbDateTime.Now,
        };
        await _workflows.AddStepAsync(step);
        return step;
    }

    private static bool TriggerMatches(WorkflowDefinitionVersion version, string eventType)
    {
        WorkflowTriggerModel? trigger = Deserialize<WorkflowTriggerModel>(version.TriggerJson);
        return trigger is not null && string.Equals(trigger.EventType, eventType, StringComparison.OrdinalIgnoreCase);
    }

    private DateTime NextRetry(int attempt)
    {
        double minutes = Math.Min(60, Math.Pow(2, Math.Max(0, attempt)));
        return DbDateTime.Now.AddMinutes(minutes);
    }

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

    private static string Serialize(object? value)
        => JsonSerializer.Serialize(value, JsonOptions);
}
