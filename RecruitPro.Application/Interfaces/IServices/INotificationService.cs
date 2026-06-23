using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface INotificationService
{
    Task<ApiResponse<NotificationListResponseDto>> GetUserNotificationsAsync(Guid userId, int page, int pageSize);
    Task<ApiResponse<NotificationUnreadCountDto>> GetUnreadCountAsync(Guid userId);
    Task<ApiResponse<NotificationDto>> MarkAsReadAsync(Guid userId, string notificationId);
    Task<ApiResponse<NotificationUnreadCountDto>> MarkAllAsReadAsync(Guid userId);
}
