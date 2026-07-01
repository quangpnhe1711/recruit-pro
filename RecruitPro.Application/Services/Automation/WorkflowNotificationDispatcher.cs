using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Services.Automation;

/// <summary>
/// Notification sink for workflow actions. Deduplicates recipients and drops unknown users. In Shadow
/// mode it resolves-and-returns recipients WITHOUT persisting or pushing (would-notify). In Live mode it
/// persists one notification per recipient and pushes over SSE — reusing the same shapes as the direct
/// notification path so a workflow-sent item is indistinguishable to the frontend.
/// </summary>
public class WorkflowNotificationDispatcher : IWorkflowNotificationDispatcher
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationRealtimeSender _realtimeSender;
    private readonly IUnitOfWork _unitOfWork;

    public WorkflowNotificationDispatcher(
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        INotificationRealtimeSender realtimeSender,
        IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _realtimeSender = realtimeSender;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Guid>> DispatchAsync(
        IReadOnlyCollection<Guid> userIds,
        string eventCode,
        string title,
        string body,
        string? dataJson,
        WorkflowMode mode,
        CancellationToken cancellationToken = default)
    {
        List<Guid> recipients = [];
        foreach (Guid userId in userIds.Distinct())
        {
            if (userId == Guid.Empty)
            {
                continue;
            }
            User? user = await _userRepository.GetByIdAsync(userId);
            if (user is not null)
            {
                recipients.Add(userId);
            }
        }

        // Shadow (or Disabled) never sends: recipients are the would-notify set only.
        if (mode != WorkflowMode.Live || recipients.Count == 0)
        {
            return recipients;
        }

        List<Notification> notifications = recipients.Select(userId => new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventCode = eventCode,
            Title = title,
            Body = body,
            Content = body,
            DataJson = dataJson,
            Type = "Automation",
            IsRead = false,
            IsSeen = false,
            CreatedAt = DbDateTime.Now,
        }).ToList();

        await _notificationRepository.AddRangeAsync(notifications);
        await _unitOfWork.SaveChangesAsync();

        object? data = ParseData(dataJson);
        foreach (Notification notification in notifications)
        {
            await _realtimeSender.SendToUserAsync(notification.UserId, new NotificationDto
            {
                Id = notification.Id,
                UserId = notification.UserId,
                EventCode = eventCode,
                Title = title,
                Body = body,
                Type = "Automation",
                Data = data,
                IsRead = false,
                IsSeen = false,
                CreatedAt = notification.CreatedAt ?? DbDateTime.Now,
            }, cancellationToken);
        }

        return recipients;
    }

    private static object? ParseData(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(dataJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
