using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RecruitPro.Application.Automation;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Request.Automation;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Automation;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Services.Automation;

public class WorkflowDefinitionService : IWorkflowDefinitionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWorkflowRepository _workflows;
    private readonly IUnitOfWork _unitOfWork;

    public WorkflowDefinitionService(IWorkflowRepository workflows, IUnitOfWork unitOfWork)
    {
        _workflows = workflows;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<AutomationDashboardDto>> GetDashboardAsync()
    {
        IReadOnlyList<WorkflowDefinition> definitions = await _workflows.ListDefinitionsAsync();
        IReadOnlyList<WorkflowExecution> recent = await _workflows.GetRecentExecutionsAsync(10);

        string? mostCommonFailedAction = recent
            .Where(e => e.Status is WorkflowExecutionStatus.Failed or WorkflowExecutionStatus.DeadLetter)
            .Select(e => e.ErrorReason)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .GroupBy(r => r)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        var dto = new AutomationDashboardDto
        {
            TotalWorkflows = definitions.Count,
            EnabledWorkflows = definitions.Count(d => d.IsEnabled),
            ExecutionsToday = await _workflows.CountExecutionsSinceAsync(DbDateTime.Today),
            FailedExecutions = await _workflows.CountExecutionsByStatusAsync(WorkflowExecutionStatus.Failed),
            DeadLetterCount = await _workflows.CountUnresolvedDeadLettersAsync(),
            MostCommonFailedAction = mostCommonFailedAction,
            RecentExecutions = recent.Select(AutomationMapper.ToExecutionSummary).ToList(),
        };
        return ApiResponse<AutomationDashboardDto>.Ok(dto);
    }

    public async Task<ApiResponse<List<WorkflowSummaryDto>>> ListAsync(bool? isEnabled, string? triggerEventType, string? mode)
    {
        IReadOnlyList<WorkflowDefinition> definitions = await _workflows.ListDefinitionsAsync();
        IReadOnlyList<WorkflowExecution> recent = await _workflows.GetRecentExecutionsAsync(200);
        Dictionary<Guid, WorkflowExecution> lastRun = recent
            .GroupBy(e => e.WorkflowDefinitionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.CreatedAt).First());

        List<WorkflowSummaryDto> items = [];
        foreach (WorkflowDefinition d in definitions)
        {
            WorkflowDefinitionVersion? active = d.Versions.FirstOrDefault(v => v.IsActive);
            string? triggerEvent = active is null ? null : ParseTrigger(active.TriggerJson);
            string modeLabel = active?.Mode.ToString() ?? WorkflowMode.Disabled.ToString();

            if (isEnabled.HasValue && d.IsEnabled != isEnabled.Value) continue;
            if (!string.IsNullOrWhiteSpace(triggerEventType) && !string.Equals(triggerEvent, triggerEventType, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(mode) && !string.Equals(modeLabel, mode, StringComparison.OrdinalIgnoreCase)) continue;

            lastRun.TryGetValue(d.Id, out WorkflowExecution? last);
            items.Add(new WorkflowSummaryDto
            {
                Id = d.Id.ToString(),
                Name = d.Name,
                IsEnabled = d.IsEnabled,
                ActiveVersionNo = active?.VersionNo,
                TriggerEventType = triggerEvent,
                Mode = modeLabel,
                LastRunAt = last?.CreatedAt,
                LastStatus = last?.Status.ToString(),
            });
        }
        return ApiResponse<List<WorkflowSummaryDto>>.Ok(items);
    }

    public async Task<ApiResponse<WorkflowDetailDto>> GetAsync(string id)
    {
        if (!Guid.TryParse(id, out Guid guid))
        {
            return ApiResponse<WorkflowDetailDto>.BadRequest(ErrorCodes.InvalidInput);
        }
        WorkflowDefinition? definition = await _workflows.GetDefinitionAsync(guid);
        if (definition is null)
        {
            return ApiResponse<WorkflowDetailDto>.NotFound(ErrorCodes.WorkflowNotFound);
        }

        var executions = await _workflows.QueryExecutionsAsync(guid, null, null, null, null, null, 1, 10);
        return ApiResponse<WorkflowDetailDto>.Ok(ToDetail(definition, executions.Items));
    }

    public async Task<ApiResponse<WorkflowDetailDto>> CreateAsync(CreateWorkflowRequest request, Guid? userId)
    {
        string? validation = Validate(request.Name, request.TriggerEventType, request.Mode, request.Actions, request.Conditions);
        if (validation is not null)
        {
            return ApiResponse<WorkflowDetailDto>.UnprocessableEntity(validation);
        }
        if (await _workflows.DefinitionExistsByNameAsync(request.Name))
        {
            return ApiResponse<WorkflowDetailDto>.Conflict(ErrorCodes.DuplicateEntity);
        }

        WorkflowMode mode = Enum.Parse<WorkflowMode>(request.Mode, true);
        Guid defId = Guid.NewGuid();
        WorkflowDefinition definition = new()
        {
            Id = defId,
            Name = request.Name.Trim(),
            Description = request.Description,
            IsEnabled = true,
            CreatedBy = userId,
            CreatedAt = DbDateTime.Now,
        };
        WorkflowDefinitionVersion version = BuildVersion(defId, 1, request.TriggerEventType, request.Conditions, request.Actions, mode);

        await _workflows.AddDefinitionAsync(definition);
        await _workflows.AddVersionAsync(version);
        await _unitOfWork.SaveChangesAsync();

        definition.Versions.Add(version);
        return ApiResponse<WorkflowDetailDto>.Created(ToDetail(definition, []));
    }

    public async Task<ApiResponse<WorkflowDetailDto>> UpdateAsync(string id, UpdateWorkflowRequest request, Guid? userId)
    {
        if (!Guid.TryParse(id, out Guid guid))
        {
            return ApiResponse<WorkflowDetailDto>.BadRequest(ErrorCodes.InvalidInput);
        }
        WorkflowDefinition? definition = await _workflows.GetDefinitionTrackedAsync(guid);
        if (definition is null)
        {
            return ApiResponse<WorkflowDetailDto>.NotFound(ErrorCodes.WorkflowNotFound);
        }

        // Base content = latest version (draft or published) so omitted fields are preserved.
        WorkflowDefinitionVersion latest = definition.Versions.OrderByDescending(v => v.VersionNo).First();
        string triggerEvent = request.TriggerEventType ?? ParseTrigger(latest.TriggerJson) ?? string.Empty;
        string modeStr = request.Mode ?? latest.Mode.ToString();
        List<WorkflowConditionInput> conditions = request.Conditions ?? ParseConditionInputs(latest.ConditionsJson);
        List<WorkflowActionInput> actions = request.Actions ?? ParseActionInputs(latest.ActionsJson);

        string? validation = Validate(request.Name ?? definition.Name, triggerEvent, modeStr, actions, conditions);
        if (validation is not null)
        {
            return ApiResponse<WorkflowDetailDto>.UnprocessableEntity(validation);
        }

        if (!string.IsNullOrWhiteSpace(request.Name)) definition.Name = request.Name.Trim();
        if (request.Description is not null) definition.Description = request.Description;
        definition.UpdatedAt = DbDateTime.Now;

        WorkflowMode mode = Enum.Parse<WorkflowMode>(modeStr, true);

        // A published version is immutable: mutate the existing unpublished draft, else create a new one.
        WorkflowDefinitionVersion? draft = definition.Versions
            .Where(v => v.PublishedAt == null)
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefault();

        if (draft is null)
        {
            int nextNo = definition.Versions.Max(v => v.VersionNo) + 1;
            draft = BuildVersion(definition.Id, nextNo, triggerEvent, conditions, actions, mode);
            await _workflows.AddVersionAsync(draft);
            definition.Versions.Add(draft);
        }
        else
        {
            WorkflowDefinitionVersion tracked = await _workflows.GetVersionTrackedAsync(draft.Id) ?? draft;
            ApplyContent(tracked, triggerEvent, conditions, actions, mode);
        }

        await _unitOfWork.SaveChangesAsync();

        var executions = await _workflows.QueryExecutionsAsync(guid, null, null, null, null, null, 1, 10);
        return ApiResponse<WorkflowDetailDto>.Ok(ToDetail(definition, executions.Items));
    }

    public async Task<ApiResponse<WorkflowVersionDto>> PublishAsync(string id, Guid? userId)
    {
        if (!Guid.TryParse(id, out Guid guid))
        {
            return ApiResponse<WorkflowVersionDto>.BadRequest(ErrorCodes.InvalidInput);
        }
        WorkflowDefinition? definition = await _workflows.GetDefinitionTrackedAsync(guid);
        if (definition is null)
        {
            return ApiResponse<WorkflowVersionDto>.NotFound(ErrorCodes.WorkflowNotFound);
        }

        WorkflowDefinitionVersion? draft = definition.Versions
            .Where(v => v.PublishedAt == null)
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefault();
        if (draft is null)
        {
            return ApiResponse<WorkflowVersionDto>.UnprocessableEntity(ErrorCodes.BusinessRuleViolation);
        }

        WorkflowDefinitionVersion trackedDraft = await _workflows.GetVersionTrackedAsync(draft.Id) ?? draft;

        // Deactivate the currently active version (immutable content, just flip the flag).
        foreach (WorkflowDefinitionVersion v in definition.Versions.Where(v => v.IsActive))
        {
            WorkflowDefinitionVersion tracked = await _workflows.GetVersionTrackedAsync(v.Id) ?? v;
            tracked.IsActive = false;
        }

        trackedDraft.IsActive = true;
        trackedDraft.PublishedAt = DbDateTime.Now;
        trackedDraft.PublishedBy = userId;
        definition.ActiveVersionId = trackedDraft.Id;
        definition.UpdatedAt = DbDateTime.Now;

        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<WorkflowVersionDto>.Ok(AutomationMapper.ToVersionDto(trackedDraft));
    }

    public async Task<ApiResponse<WorkflowDetailDto>> SetEnabledAsync(string id, bool isEnabled)
    {
        if (!Guid.TryParse(id, out Guid guid))
        {
            return ApiResponse<WorkflowDetailDto>.BadRequest(ErrorCodes.InvalidInput);
        }
        WorkflowDefinition? definition = await _workflows.GetDefinitionTrackedAsync(guid);
        if (definition is null)
        {
            return ApiResponse<WorkflowDetailDto>.NotFound(ErrorCodes.WorkflowNotFound);
        }

        definition.IsEnabled = isEnabled;
        definition.UpdatedAt = DbDateTime.Now;
        await _unitOfWork.SaveChangesAsync();

        var executions = await _workflows.QueryExecutionsAsync(guid, null, null, null, null, null, 1, 10);
        return ApiResponse<WorkflowDetailDto>.Ok(ToDetail(definition, executions.Items));
    }

    // ---------------- helpers ----------------

    private static WorkflowDetailDto ToDetail(WorkflowDefinition d, IReadOnlyList<WorkflowExecution> executions)
    {
        WorkflowDefinitionVersion? active = d.Versions.FirstOrDefault(v => v.IsActive);
        return new WorkflowDetailDto
        {
            Id = d.Id.ToString(),
            Name = d.Name,
            Description = d.Description,
            IsEnabled = d.IsEnabled,
            ActiveVersion = active is null ? null : AutomationMapper.ToVersionDto(active),
            Versions = d.Versions.OrderByDescending(v => v.VersionNo).Select(AutomationMapper.ToVersionDto).ToList(),
            RecentExecutions = executions.Select(AutomationMapper.ToExecutionSummary).ToList(),
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
        };
    }

    private static WorkflowDefinitionVersion BuildVersion(
        Guid defId, int versionNo, string triggerEvent,
        List<WorkflowConditionInput> conditions, List<WorkflowActionInput> actions, WorkflowMode mode)
    {
        WorkflowDefinitionVersion version = new()
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = defId,
            VersionNo = versionNo,
            IsActive = false,
            CreatedAt = DbDateTime.Now,
        };
        ApplyContent(version, triggerEvent, conditions, actions, mode);
        return version;
    }

    private static void ApplyContent(
        WorkflowDefinitionVersion version, string triggerEvent,
        List<WorkflowConditionInput> conditions, List<WorkflowActionInput> actions, WorkflowMode mode)
    {
        version.TriggerJson = JsonSerializer.Serialize(new WorkflowTriggerModel { EventType = triggerEvent }, JsonOptions);
        version.ConditionsJson = JsonSerializer.Serialize(
            conditions.Select(c => new { field = c.Field, @operator = c.Operator, value = c.Value }), JsonOptions);
        version.ActionsJson = SerializeActions(actions);
        version.Mode = mode;
    }

    private static string SerializeActions(List<WorkflowActionInput> actions)
    {
        // Build [{ "type": ..., "config": {...} }] embedding each action's raw config object.
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (WorkflowActionInput a in actions)
            {
                writer.WriteStartObject();
                writer.WriteString("type", a.Type);
                writer.WritePropertyName("config");
                using JsonDocument cfg = JsonDocument.Parse(string.IsNullOrWhiteSpace(a.ConfigJson) ? "{}" : a.ConfigJson);
                cfg.RootElement.WriteTo(writer);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string? ParseTrigger(string triggerJson)
    {
        try
        {
            return JsonSerializer.Deserialize<WorkflowTriggerModel>(triggerJson, JsonOptions)?.EventType;
        }
        catch (JsonException) { return null; }
    }

    private static List<WorkflowConditionInput> ParseConditionInputs(string conditionsJson)
    {
        try
        {
            List<WorkflowConditionModel> models = JsonSerializer.Deserialize<List<WorkflowConditionModel>>(conditionsJson, JsonOptions) ?? [];
            return models.Select(m => new WorkflowConditionInput { Field = m.Field, Operator = m.Operator, Value = m.Value }).ToList();
        }
        catch (JsonException) { return []; }
    }

    private static List<WorkflowActionInput> ParseActionInputs(string actionsJson)
    {
        try
        {
            List<WorkflowActionModel> models = JsonSerializer.Deserialize<List<WorkflowActionModel>>(actionsJson, JsonOptions) ?? [];
            return models.Select(m => new WorkflowActionInput
            {
                Type = m.Type,
                ConfigJson = m.Config.ValueKind == JsonValueKind.Undefined ? "{}" : m.Config.GetRawText()
            }).ToList();
        }
        catch (JsonException) { return []; }
    }

    // Returns an ErrorCodes.* code when the definition is invalid, null when valid.
    // ponytail: all branches collapse to ValidationFailed — no workflow-specific validation codes exist yet
    // (see report for proposed per-branch codes); add them if the FE needs to distinguish these cases.
    private static string? Validate(
        string? name, string triggerEvent, string mode,
        List<WorkflowActionInput> actions, List<WorkflowConditionInput> conditions)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ErrorCodes.ValidationFailed;
        }
        if (!WorkflowEventTypes.All.Contains(triggerEvent))
        {
            return ErrorCodes.ValidationFailed;
        }
        if (!Enum.TryParse<WorkflowMode>(mode, true, out _))
        {
            return ErrorCodes.ValidationFailed;
        }
        if (actions is null || actions.Count == 0)
        {
            return ErrorCodes.ValidationFailed;
        }
        foreach (WorkflowActionInput a in actions)
        {
            if (!WorkflowActionType.All.Contains(a.Type))
            {
                return ErrorCodes.ValidationFailed;
            }
            if (!IsValidJson(a.ConfigJson))
            {
                return ErrorCodes.ValidationFailed;
            }
        }
        foreach (WorkflowConditionInput c in conditions ?? [])
        {
            if (!WorkflowConditionOperator.All.Contains(c.Operator))
            {
                return ErrorCodes.ValidationFailed;
            }
            if (string.IsNullOrWhiteSpace(c.Field))
            {
                return ErrorCodes.ValidationFailed;
            }
        }
        return null;
    }

    private static bool IsValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true; // empty config -> {}
        }
        try
        {
            using JsonDocument _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
