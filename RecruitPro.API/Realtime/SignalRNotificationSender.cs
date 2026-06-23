using Microsoft.AspNetCore.SignalR;
using RecruitPro.API.Hubs;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Realtime;

public class SignalRNotificationSender : INotificationRealtimeSender
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationSender(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendToUserAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients
            .User(userId.ToString())
            .SendAsync("notification:new", notification, cancellationToken);
    }
}
