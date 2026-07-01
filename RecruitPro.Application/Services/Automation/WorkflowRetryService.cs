using System;
using System.Collections.Generic;
using System.Linq;
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
/// Re-runs the actions of a Failed execution. Success flips it to Success; another failure re-schedules
/// a retry, or dead-letters once <see cref="WorkflowAutomationSettings.MaxAttempts"/> is reached.
/// Manual retry (from the SystemAdmin UI) runs immediately regardless of next_retry_at.
/// </summary>
public class WorkflowRetryService : IWorkflowRetryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWorkflowRepository _workflows;
    private readonly IWorkflowActionRegistry _actionRegistry;
    private readonly IUnitOfWork _unitOfWork;
    private readonly WorkflowAutomationSettings _settings;
    private readonly ILogger<WorkflowRetryService> _logger;

    public WorkflowRetryService(
        IWorkflowRepository workflows,
        IWorkflowActionRegistry actionRegistry,
        IUnitOfWork unitOfWork,
        IOptions<WorkflowAutomationSettings> settings,
        ILogger<WorkflowRetryService> logger)
    {
        _workflows = workflows;
        _actionRegistry = actionRegistry;
        _unitOfWork = unitOfWork;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<WorkflowExecutionStatus> RetryExecutionAsync(Guid executionId, bool manual, CancellationToken cancellationToken = default)
    {
        WorkflowExecution? execution = await _workflows.GetExecutionTrackedAsync(executionId);
        if (execution is null)
        {
            return WorkflowExecutionStatus.Failed;
        }
        if (execution.Status is not (WorkflowExecutionStatus.Failed or WorkflowExecutionStatus.DeadLetter))
        {
            return execution.Status; // Only failed/dead-letter executions can be retried.
        }

        WorkflowDefinitionVersion? version = await _workflows.GetVersionAsync(execution.WorkflowDefinitionVersionId);
        if (version is null)
        {
            return execution.Status;
        }

        execution.Status = WorkflowExecutionStatus.Retrying;
        execution.AttemptCount += 1;
        execution.ErrorReason = null;
        execution.NextRetryAt = null;
        await _unitOfWork.SaveChangesAsync();

        int stepNo = (execution.Steps.Count == 0 ? 0 : execution.Steps.Max(s => s.StepNo)) + 1;

        using JsonDocument payloadDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(execution.InputPayloadJson) ? "{}" : execution.InputPayloadJson);
        JsonElement payload = payloadDoc.RootElement.Clone();

        List<WorkflowActionModel> actions = Deserialize<List<WorkflowActionModel>>(version.ActionsJson) ?? [];
        List<object?> outputs = [];

        foreach (WorkflowActionModel action in actions)
        {
            IWorkflowActionHandler? handler = _actionRegistry.Resolve(action.Type);
            WorkflowActionResult result;
            if (handler is null)
            {
                result = WorkflowActionResult.Fail($"Unknown action handler '{action.Type}'");
            }
            else
            {
                try
                {
                    result = await handler.ExecuteAsync(new WorkflowActionContext
                    {
                        Mode = execution.Mode,
                        EventType = execution.TriggerEventType,
                        Payload = payload,
                        Config = action.Config,
                        ExecutionId = execution.Id,
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    result = WorkflowActionResult.Fail(ex.Message);
                }
            }

            await AddStepAsync(execution.Id, stepNo++, action.Type, result);
            outputs.Add(result.Output);

            if (result.Status == WorkflowStepStatus.Failed)
            {
                return await MarkFailedAsync(execution, action.Type, result.Error ?? "action failed");
            }
        }

        execution.Status = WorkflowExecutionStatus.Success;
        execution.ErrorReason = null;
        execution.OutputJson = JsonSerializer.Serialize(new { retried = true, manual, steps = outputs }, JsonOptions);
        execution.FinishedAt = DbDateTime.Now;
        await _unitOfWork.SaveChangesAsync();
        return WorkflowExecutionStatus.Success;
    }

    public async Task ProcessDueRetriesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WorkflowExecution> due = await _workflows.GetRetryableExecutionsAsync(DbDateTime.Now, 25);
        foreach (WorkflowExecution execution in due)
        {
            try
            {
                await RetryExecutionAsync(execution.Id, manual: false, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-retry failed for execution {ExecutionId}", execution.Id);
            }
        }
    }

    private async Task<WorkflowExecutionStatus> MarkFailedAsync(WorkflowExecution execution, string actionType, string error)
    {
        bool deadLetter = execution.AttemptCount >= _settings.MaxAttempts;
        execution.Status = deadLetter ? WorkflowExecutionStatus.DeadLetter : WorkflowExecutionStatus.Failed;
        execution.ErrorReason = error;
        execution.FinishedAt = DbDateTime.Now;
        execution.NextRetryAt = deadLetter ? null : DbDateTime.Now.AddMinutes(Math.Min(60, Math.Pow(2, execution.AttemptCount)));

        await _workflows.AddDeadLetterAsync(new WorkflowActionDeadLetter
        {
            Id = Guid.NewGuid(),
            ExecutionId = execution.Id,
            ActionType = actionType,
            PayloadJson = execution.InputPayloadJson,
            ErrorReason = error,
            AttemptCount = execution.AttemptCount,
            NextRetryAt = execution.NextRetryAt,
            CreatedAt = DbDateTime.Now,
        });
        await _unitOfWork.SaveChangesAsync();
        return execution.Status;
    }

    private async Task AddStepAsync(Guid executionId, int stepNo, string actionType, WorkflowActionResult result)
    {
        await _workflows.AddStepAsync(new WorkflowExecutionStep
        {
            Id = Guid.NewGuid(),
            ExecutionId = executionId,
            StepNo = stepNo,
            StepType = WorkflowStepType.Action,
            ActionType = actionType,
            Status = result.Status,
            OutputJson = JsonSerializer.Serialize(result.Output, JsonOptions),
            ErrorReason = result.Error,
            StartedAt = DbDateTime.Now,
            FinishedAt = DbDateTime.Now,
            CreatedAt = DbDateTime.Now,
        });
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
}
