using System.Collections.Generic;
using System.Linq;
using RecruitPro.Application.Automation;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Services.Automation.Handlers;

/// <summary>
/// rule_based_next_step_suggestion: derives a deterministic Vietnamese suggestion from the payload score
/// (default field "finalScore") and notifies the configured recipients with it. No AI. Config:
/// { "scoreField":"finalScore", "recipients":["assignedRecruiter"], "eventCode","title" }.
/// </summary>
public class RuleBasedNextStepSuggestionActionHandler : IWorkflowActionHandler
{
    private readonly IWorkflowNotificationDispatcher _dispatcher;

    public RuleBasedNextStepSuggestionActionHandler(IWorkflowNotificationDispatcher dispatcher)
        => _dispatcher = dispatcher;

    public string ActionType => WorkflowActionType.RuleBasedNextStepSuggestion;

    public async Task<WorkflowActionResult> ExecuteAsync(WorkflowActionContext context, CancellationToken cancellationToken = default)
    {
        string scoreField = WorkflowPayload.GetConfigString(context.Config, "scoreField", "finalScore");
        decimal? score = WorkflowPayload.GetDecimal(context.Payload, scoreField);
        (string band, string suggestion) = NextStepSuggestion.Compute(score);

        IReadOnlyList<string> selectors = WorkflowPayload.GetStringArray(context.Config, "recipients");
        if (selectors.Count == 0)
        {
            selectors = [WorkflowRecipientSelector.AssignedRecruiter, WorkflowRecipientSelector.AssignedDepartmentHead];
        }

        IReadOnlyList<Guid> targets = WorkflowPayload.ResolveRecipients(context.Payload, selectors);
        string eventCode = WorkflowPayload.GetConfigString(context.Config, "eventCode", "workflow_next_step_suggestion");
        string title = WorkflowPayload.GetConfigString(context.Config, "title", "Gợi ý bước tiếp theo");

        IReadOnlyList<Guid> notified = targets.Count == 0
            ? []
            : await _dispatcher.DispatchAsync(targets, eventCode, title, suggestion, context.Payload.GetRawText(), context.Mode, cancellationToken);

        return WorkflowActionResult.Ok(new
        {
            band,
            suggestion,
            score,
            sent = context.Mode == WorkflowMode.Live && notified.Count > 0,
            wouldNotify = notified.Select(id => id.ToString()).ToArray()
        });
    }
}

/// <summary>shadow_log: records the would-run payload/config for verification; never sends anything.</summary>
public class ShadowLogActionHandler : IWorkflowActionHandler
{
    public string ActionType => WorkflowActionType.ShadowLog;

    public Task<WorkflowActionResult> ExecuteAsync(WorkflowActionContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(WorkflowActionResult.Ok(new
        {
            wouldRun = true,
            mode = context.Mode.ToString(),
            eventType = context.EventType,
            note = "Shadow log only — no notification sent."
        }));
    }
}
