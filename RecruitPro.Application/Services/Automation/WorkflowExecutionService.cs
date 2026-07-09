using System;
using System.Collections.Generic;
using System.Linq;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Automation;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Services.Automation;

public class WorkflowExecutionService : IWorkflowExecutionService
{
    private readonly IWorkflowRepository _workflows;
    private readonly IEventOutboxRepository _outbox;
    private readonly IWorkflowRetryService _retryService;

    public WorkflowExecutionService(
        IWorkflowRepository workflows,
        IEventOutboxRepository outbox,
        IWorkflowRetryService retryService)
    {
        _workflows = workflows;
        _outbox = outbox;
        _retryService = retryService;
    }

    public async Task<ApiResponse<PaginatedResponseDto<ExecutionSummaryDto>>> QueryAsync(
        string? workflowDefinitionId, string? status, string? eventType, string? mode,
        DateTime? from, DateTime? to, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        Guid? defId = Guid.TryParse(workflowDefinitionId, out Guid g) ? g : null;

        var (items, total) = await _workflows.QueryExecutionsAsync(defId, status, eventType, mode, from, to, page, pageSize);
        return ApiResponse<PaginatedResponseDto<ExecutionSummaryDto>>.Ok(new PaginatedResponseDto<ExecutionSummaryDto>
        {
            Items = items.Select(AutomationMapper.ToExecutionSummary).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = total,
        });
    }

    public async Task<ApiResponse<ExecutionDetailDto>> GetAsync(string id)
    {
        if (!Guid.TryParse(id, out Guid guid))
        {
            return ApiResponse<ExecutionDetailDto>.BadRequest(ErrorCodes.InvalidInput);
        }
        WorkflowExecution? execution = await _workflows.GetExecutionAsync(guid);
        if (execution is null)
        {
            return ApiResponse<ExecutionDetailDto>.NotFound(ErrorCodes.WorkflowExecutionNotFound);
        }
        return ApiResponse<ExecutionDetailDto>.Ok(await ToDetailAsync(execution));
    }

    public async Task<ApiResponse<ExecutionDetailDto>> RetryAsync(string id)
    {
        if (!Guid.TryParse(id, out Guid guid))
        {
            return ApiResponse<ExecutionDetailDto>.BadRequest(ErrorCodes.InvalidInput);
        }
        WorkflowExecution? execution = await _workflows.GetExecutionAsync(guid);
        if (execution is null)
        {
            return ApiResponse<ExecutionDetailDto>.NotFound(ErrorCodes.WorkflowExecutionNotFound);
        }
        if (execution.Status is not (WorkflowExecutionStatus.Failed or WorkflowExecutionStatus.DeadLetter))
        {
            return ApiResponse<ExecutionDetailDto>.UnprocessableEntity(ErrorCodes.BusinessRuleViolation);
        }

        await _retryService.RetryExecutionAsync(guid, manual: true);

        WorkflowExecution refreshed = (await _workflows.GetExecutionAsync(guid))!;
        return ApiResponse<ExecutionDetailDto>.Ok(await ToDetailAsync(refreshed));
    }

    public async Task<ApiResponse<PaginatedResponseDto<OutboxEventDto>>> QueryEventsAsync(
        string? status, string? eventType, DateTime? from, DateTime? to, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var (items, total) = await _outbox.QueryAsync(status, eventType, from, to, page, pageSize);
        return ApiResponse<PaginatedResponseDto<OutboxEventDto>>.Ok(new PaginatedResponseDto<OutboxEventDto>
        {
            Items = items.Select(ToEventDto).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = total,
        });
    }

    public async Task<ApiResponse<OutboxEventDto>> GetEventAsync(string id)
    {
        if (!Guid.TryParse(id, out Guid guid))
        {
            return ApiResponse<OutboxEventDto>.BadRequest(ErrorCodes.InvalidInput);
        }
        PublishedDomainEvent? domainEvent = await _outbox.GetByIdAsync(guid);
        if (domainEvent is null)
        {
            return ApiResponse<OutboxEventDto>.NotFound(ErrorCodes.EntityNotFound);
        }
        return ApiResponse<OutboxEventDto>.Ok(ToEventDto(domainEvent));
    }

    private async Task<ExecutionDetailDto> ToDetailAsync(WorkflowExecution execution)
    {
        ExecutionSummaryDto summary = AutomationMapper.ToExecutionSummary(execution);
        WorkflowDefinitionVersion? version = await _workflows.GetVersionAsync(execution.WorkflowDefinitionVersionId);

        return new ExecutionDetailDto
        {
            Id = summary.Id,
            WorkflowDefinitionId = summary.WorkflowDefinitionId,
            WorkflowName = summary.WorkflowName,
            EventType = summary.EventType,
            Mode = summary.Mode,
            Status = summary.Status,
            StartedAt = summary.StartedAt,
            FinishedAt = summary.FinishedAt,
            DurationMs = summary.DurationMs,
            AttemptCount = summary.AttemptCount,
            ErrorReason = summary.ErrorReason,
            RetryAvailable = summary.RetryAvailable,
            CreatedAt = summary.CreatedAt,
            InputPayloadJson = execution.InputPayloadJson,
            OutputJson = execution.OutputJson,
            VersionSnapshot = version is null ? null : AutomationMapper.ToVersionDto(version),
            Steps = execution.Steps
                .OrderBy(s => s.StepNo)
                .Select(s => new WorkflowStepDto
                {
                    Id = s.Id.ToString(),
                    StepNo = s.StepNo,
                    StepType = s.StepType,
                    ActionType = s.ActionType,
                    Status = s.Status.ToString(),
                    InputJson = s.InputJson,
                    OutputJson = s.OutputJson,
                    ErrorReason = s.ErrorReason,
                    StartedAt = s.StartedAt,
                    FinishedAt = s.FinishedAt,
                }).ToList(),
        };
    }

    private static OutboxEventDto ToEventDto(PublishedDomainEvent e) => new()
    {
        Id = e.Id.ToString(),
        EventType = e.EventType,
        AggregateType = e.AggregateType,
        AggregateId = e.AggregateId.ToString(),
        DedupKey = e.DedupKey,
        Status = e.Status.ToString(),
        OccurredAt = e.OccurredAt,
        ProcessedAt = e.ProcessedAt,
        AttemptCount = e.AttemptCount,
        ErrorReason = e.ErrorReason,
        PayloadJson = e.PayloadJson,
    };
}
