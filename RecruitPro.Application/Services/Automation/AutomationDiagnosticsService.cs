using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Common;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Automation;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Services.Automation;

/// <summary>
/// Read-only diagnostics for the SystemAdmin automation screens: is the worker alive, are events piling up,
/// and — per workflow — the one human reason nothing ran. This is what turns the "dead function" screen into
/// something an operator can actually reason about.
/// </summary>
public class AutomationDiagnosticsService : IAutomationDiagnosticsService
{
    // A heartbeat older than this means the worker is not looping (loop interval is 5s).
    private const double StaleSeconds = 60;

    private readonly IWorkflowRepository _workflows;
    private readonly IEventOutboxRepository _outbox;
    private readonly WorkflowAutomationSettings _settings;

    public AutomationDiagnosticsService(
        IWorkflowRepository workflows,
        IEventOutboxRepository outbox,
        IOptions<WorkflowAutomationSettings> settings)
    {
        _workflows = workflows;
        _outbox = outbox;
        _settings = settings.Value;
    }

    public async Task<ApiResponse<AutomationDiagnosticsDto>> GetGlobalAsync()
    {
        DateTime now = DbDateTime.Now;
        IReadOnlyList<WorkerHeartbeat> beats = await _workflows.GetHeartbeatsAsync();
        IReadOnlyList<WorkflowDefinition> definitions = await _workflows.ListDefinitionsAsync();

        List<WorkerHeartbeatDto> workers = beats.Select(b =>
        {
            double secs = (now - b.LastBeatAt).TotalSeconds;
            return new WorkerHeartbeatDto
            {
                Name = b.WorkerName,
                LastBeatAt = b.LastBeatAt,
                SecondsSinceBeat = Math.Round(secs, 1),
                IsStale = secs > StaleSeconds,
                Status = b.Status,
                Detail = b.Detail,
            };
        }).ToList();

        WorkerHeartbeatDto? dispatcher = workers.FirstOrDefault(w => w.Name == "dispatcher");
        bool dispatcherHealthy = dispatcher is { IsStale: false };

        int pending = await _outbox.CountByStatusAsync(WorkflowEventStatus.Pending);
        PublishedDomainEvent? latestEvent = await _outbox.GetLatestAsync();
        WorkflowExecution? latestExec = await _workflows.GetLatestExecutionAsync();

        var dto = new AutomationDiagnosticsDto
        {
            AutomationEnabled = _settings.Enabled,
            DefaultMode = _settings.DefaultMode,
            Workers = workers,
            DispatcherHealthy = dispatcherHealthy,
            PendingEvents = pending,
            ProcessingEvents = await _outbox.CountByStatusAsync(WorkflowEventStatus.Processing),
            FailedEvents = await _outbox.CountByStatusAsync(WorkflowEventStatus.Failed),
            DeadLetterEvents = await _outbox.CountByStatusAsync(WorkflowEventStatus.DeadLetter),
            ExecutionsToday = await _workflows.CountExecutionsSinceAsync(DbDateTime.Today),
            FailedExecutions = await _workflows.CountExecutionsByStatusAsync(WorkflowExecutionStatus.Failed),
            UnresolvedDeadLetters = await _workflows.CountUnresolvedDeadLettersAsync(),
            LatestEvent = latestEvent is null ? null : new DiagnosticsEventDto
            {
                EventType = latestEvent.EventType,
                Status = latestEvent.Status.ToString(),
                OccurredAt = latestEvent.OccurredAt,
            },
            LatestExecution = latestExec is null ? null : new DiagnosticsExecutionDto
            {
                WorkflowName = latestExec.WorkflowDefinition?.Name ?? "(?)",
                Status = latestExec.Status.ToString(),
                CreatedAt = latestExec.CreatedAt,
            },
        };

        // Warnings — plain Vietnamese, ordered most-actionable first.
        if (!_settings.Enabled)
            dto.Warnings.Add("Tự động hóa đang bị tắt toàn hệ thống (WorkflowAutomation.Enabled = false).");
        if (dispatcher is null)
            dto.Warnings.Add("Chưa ghi nhận heartbeat của worker — worker có thể chưa chạy.");
        else if (dispatcher.IsStale)
            dto.Warnings.Add("Worker chưa chạy hoặc heartbeat đã quá hạn.");
        if (pending > 0)
            dto.Warnings.Add($"Có {pending} sự kiện đang chờ xử lý.");
        if (definitions.Any(d => d.IsEnabled && !d.Versions.Any(v => v.IsActive)))
            dto.Warnings.Add("Có workflow đang bật nhưng chưa có phiên bản active.");
        if (definitions.Any(d => d.IsEnabled && d.Versions.Any(v => v.IsActive && v.Mode == WorkflowMode.Shadow)))
            dto.Warnings.Add("Một số workflow đang ở chế độ Shadow nên chỉ ghi log, chưa gửi thông báo thật.");

        return ApiResponse<AutomationDiagnosticsDto>.Ok(dto);
    }

    public async Task<ApiResponse<WorkflowDiagnosticsDto>> GetForWorkflowAsync(string workflowId)
    {
        if (!Guid.TryParse(workflowId, out Guid id))
        {
            return ApiResponse<WorkflowDiagnosticsDto>.BadRequest("workflowId không hợp lệ.");
        }

        WorkflowDefinition? def = await _workflows.GetDefinitionAsync(id);
        if (def is null)
        {
            return ApiResponse<WorkflowDiagnosticsDto>.NotFound("Không tìm thấy workflow.");
        }

        WorkflowDefinitionVersion? active = def.Versions.FirstOrDefault(v => v.IsActive);
        string? triggerType = active is null ? null : ParseTriggerEventType(active.TriggerJson);
        WorkflowMode effectiveMode = triggerType is null ? WorkflowMode.Disabled : _settings.ResolveMode(triggerType);
        DateTime today = DbDateTime.Today;

        int eventsToday = triggerType is null ? 0 : await _outbox.CountByEventTypeSinceAsync(triggerType, today);
        int pendingOfType = triggerType is null ? 0
            : await _outbox.CountByEventTypeAndStatusSinceAsync(triggerType, WorkflowEventStatus.Pending, today);
        int execToday = await _workflows.CountExecutionsSinceForWorkflowAsync(id, today);
        int success = await _workflows.CountExecutionsByStatusSinceForWorkflowAsync(id, WorkflowExecutionStatus.Success, today);
        int failed = await _workflows.CountExecutionsByStatusSinceForWorkflowAsync(id, WorkflowExecutionStatus.Failed, today);
        int skipped = await _workflows.CountExecutionsByStatusSinceForWorkflowAsync(id, WorkflowExecutionStatus.Skipped, today);

        PublishedDomainEvent? latestEvent = triggerType is null ? null : await _outbox.GetLatestByEventTypeAsync(triggerType);
        WorkflowExecution? latestExec = await _workflows.GetLatestExecutionForWorkflowAsync(id);

        var dto = new WorkflowDiagnosticsDto
        {
            Id = def.Id.ToString(),
            Name = def.Name,
            IsEnabled = def.IsEnabled,
            HasActiveVersion = active is not null,
            VersionMode = active?.Mode.ToString(),
            EffectiveMode = effectiveMode.ToString(),
            TriggerEventType = triggerType,
            EventsTodayOfType = eventsToday,
            PendingEventsOfType = pendingOfType,
            ExecutionsToday = execToday,
            SuccessCount = success,
            FailedCount = failed,
            SkippedCount = skipped,
            LatestMatchingEvent = latestEvent is null ? null : new DiagnosticsEventDto
            {
                EventType = latestEvent.EventType,
                Status = latestEvent.Status.ToString(),
                OccurredAt = latestEvent.OccurredAt,
            },
            LatestExecution = latestExec is null ? null : new DiagnosticsExecutionDto
            {
                WorkflowName = latestExec.WorkflowDefinition?.Name ?? def.Name,
                Status = latestExec.Status.ToString(),
                CreatedAt = latestExec.CreatedAt,
            },
            NoExecutionReason = AutomationDiagnosticsReasoner.ResolveNoExecutionReason(
                _settings.Enabled, def.IsEnabled, active is not null, effectiveMode,
                eventsToday, execToday, pendingOfType, skipped, success),
        };

        return ApiResponse<WorkflowDiagnosticsDto>.Ok(dto);
    }

    private static string? ParseTriggerEventType(string triggerJson)
    {
        if (string.IsNullOrWhiteSpace(triggerJson)) return null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(triggerJson);
            return doc.RootElement.TryGetProperty("eventType", out JsonElement el) ? el.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
