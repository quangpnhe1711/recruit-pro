using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface INotificationRealtimeSender
{
    Task SendToUserAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);
}
