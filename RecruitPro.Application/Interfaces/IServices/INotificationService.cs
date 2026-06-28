using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface INotificationService
{
    Task<ApiResponse<NotificationListResponseDto>> GetUserNotificationsAsync(Guid userId, int page, int pageSize);
    Task<ApiResponse<NotificationCountsDto>> GetCountsAsync(Guid userId);
    Task<ApiResponse<NotificationDto>> MarkAsReadAsync(Guid userId, string notificationId);
    Task<ApiResponse<NotificationCountsDto>> MarkAllAsSeenAsync(Guid userId);
    Task<ApiResponse<NotificationCountsDto>> MarkAllAsReadAsync(Guid userId);
}
