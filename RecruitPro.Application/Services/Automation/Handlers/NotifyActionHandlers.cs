using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RecruitPro.Application.Automation;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Services.Automation.Handlers;

/// <summary>
/// notify_user: notifies specific ownership-resolved recipients from the payload. Config:
/// { "recipients": ["assignedRecruiter","assignedDepartmentHead","candidate"], "eventCode","title","body" }.
/// </summary>
public class NotifyUserActionHandler : IWorkflowActionHandler
{
    private readonly IWorkflowNotificationDispatcher _dispatcher;

    public NotifyUserActionHandler(IWorkflowNotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    public string ActionType => WorkflowActionType.NotifyUser;

    public async Task<WorkflowActionResult> ExecuteAsync(WorkflowActionContext context, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> selectors = WorkflowPayload.GetStringArray(context.Config, "recipients");
        if (selectors.Count == 0)
        {
            selectors = [WorkflowRecipientSelector.AssignedRecruiter];
        }

        IReadOnlyList<Guid> targets = WorkflowPayload.ResolveRecipients(context.Payload, selectors);
        if (targets.Count == 0)
        {
            return WorkflowActionResult.Skip(new { reason = "no recipient resolved from payload", selectors });
        }

        string eventCode = WorkflowPayload.GetConfigString(context.Config, "eventCode", "workflow_notification");
        string title = WorkflowPayload.GetConfigString(context.Config, "title", "Thông báo tự động");
        string body = WorkflowPayload.GetConfigString(context.Config, "body", "Có cập nhật mới trong quy trình tuyển dụng.");

        IReadOnlyList<Guid> notified = await _dispatcher.DispatchAsync(
            targets, eventCode, title, body, context.Payload.GetRawText(), context.Mode, cancellationToken);

        return WorkflowActionResult.Ok(new
        {
            sent = context.Mode == WorkflowMode.Live,
            wouldNotify = notified.Select(id => id.ToString()).ToArray(),
            recipientCount = notified.Count
        });
    }
}

/// <summary>
/// notify_role: resolves users by role and notifies them (deduped). Config:
/// { "roles": ["HR","Manager"], "eventCode","title","body" }.
/// </summary>
public class NotifyRoleActionHandler : IWorkflowActionHandler
{
    private readonly IWorkflowNotificationDispatcher _dispatcher;
    private readonly IUserRepository _userRepository;

    public NotifyRoleActionHandler(IWorkflowNotificationDispatcher dispatcher, IUserRepository userRepository)
    {
        _dispatcher = dispatcher;
        _userRepository = userRepository;
    }

    public string ActionType => WorkflowActionType.NotifyRole;

    public async Task<WorkflowActionResult> ExecuteAsync(WorkflowActionContext context, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> roles = WorkflowPayload.GetStringArray(context.Config, "roles");
        if (roles.Count == 0)
        {
            return WorkflowActionResult.Skip(new { reason = "no roles configured" });
        }

        var users = await _userRepository.GetUsersInRolesAsync(roles.ToArray());
        List<Guid> targets = users.Select(u => u.Id).Distinct().ToList();
        if (targets.Count == 0)
        {
            return WorkflowActionResult.Skip(new { reason = "no users in roles", roles });
        }

        string eventCode = WorkflowPayload.GetConfigString(context.Config, "eventCode", "workflow_role_notification");
        string title = WorkflowPayload.GetConfigString(context.Config, "title", "Thông báo tự động");
        string body = WorkflowPayload.GetConfigString(context.Config, "body", "Có cập nhật mới trong quy trình tuyển dụng.");

        IReadOnlyList<Guid> notified = await _dispatcher.DispatchAsync(
            targets, eventCode, title, body, context.Payload.GetRawText(), context.Mode, cancellationToken);

        return WorkflowActionResult.Ok(new
        {
            sent = context.Mode == WorkflowMode.Live,
            roles,
            wouldNotify = notified.Select(id => id.ToString()).ToArray(),
            recipientCount = notified.Count
        });
    }
}

/// <summary>
/// send_reminder: like notify_user but names a cooldown. Duplicate suppression is primarily enforced by
/// event-level dedup (the HeadReviewOverdue window key), so this handler notifies and records the cooldown
/// contract in its output. Config adds { "cooldownHours": 24 }.
/// </summary>
public class SendReminderActionHandler : IWorkflowActionHandler
{
    private readonly IWorkflowNotificationDispatcher _dispatcher;

    public SendReminderActionHandler(IWorkflowNotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    public string ActionType => WorkflowActionType.SendReminder;

    public async Task<WorkflowActionResult> ExecuteAsync(WorkflowActionContext context, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> selectors = WorkflowPayload.GetStringArray(context.Config, "recipients");
        if (selectors.Count == 0)
        {
            selectors = [WorkflowRecipientSelector.AssignedDepartmentHead];
        }

        IReadOnlyList<Guid> targets = WorkflowPayload.ResolveRecipients(context.Payload, selectors);
        if (targets.Count == 0)
        {
            return WorkflowActionResult.Skip(new { reason = "no recipient resolved from payload", selectors });
        }

        int cooldownHours = WorkflowPayload.GetConfigInt(context.Config, "cooldownHours", 24);
        string eventCode = WorkflowPayload.GetConfigString(context.Config, "eventCode", "workflow_reminder");
        string title = WorkflowPayload.GetConfigString(context.Config, "title", "Nhắc việc");
        string body = WorkflowPayload.GetConfigString(context.Config, "body", "Bạn có công việc đang chờ xử lý.");

        IReadOnlyList<Guid> notified = await _dispatcher.DispatchAsync(
            targets, eventCode, title, body, context.Payload.GetRawText(), context.Mode, cancellationToken);

        return WorkflowActionResult.Ok(new
        {
            sent = context.Mode == WorkflowMode.Live,
            cooldownHours,
            wouldNotify = notified.Select(id => id.ToString()).ToArray(),
            recipientCount = notified.Count
        });
    }
}
