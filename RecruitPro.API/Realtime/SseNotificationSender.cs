using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Realtime;

/// <summary>
/// <see cref="INotificationRealtimeSender"/> backed by SSE. The notification publisher
/// (<see cref="RecruitPro.Application.Services.NotificationEventService"/>) keeps depending on the
/// transport-agnostic <see cref="INotificationRealtimeSender"/>; this implementation simply forwards
/// each persisted notification to the user-scoped <see cref="INotificationSseBroker"/>.
/// </summary>
public sealed class SseNotificationSender : INotificationRealtimeSender
{
    private readonly INotificationSseBroker _broker;

    public SseNotificationSender(INotificationSseBroker broker)
    {
        _broker = broker;
    }

    public Task SendToUserAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
        => _broker.PublishAsync(userId, notification, cancellationToken);
}
