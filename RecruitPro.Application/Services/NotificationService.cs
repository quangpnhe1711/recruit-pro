using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.Application.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;

    public NotificationService(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async Task<ApiResponse<NotificationListResponseDto>> GetUserNotificationsAsync(Guid userId, int page, int pageSize)
    {
        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 1, 50);
        IReadOnlyList<Domain.Entities.Notification> notifications =
            await _notificationRepository.GetByUserIdAsync(userId, safePage, safePageSize);
        int totalItems = await _notificationRepository.CountByUserIdAsync(userId);

        return ApiResponse<NotificationListResponseDto>.Ok(new NotificationListResponseDto
        {
            Items = notifications.Select(MapNotification).ToList(),
            Meta = PaginationMetaBuilder.Build(safePage, safePageSize, totalItems)
        });
    }

    public async Task<ApiResponse<NotificationUnreadCountDto>> GetUnreadCountAsync(Guid userId)
    {
        return ApiResponse<NotificationUnreadCountDto>.Ok(new NotificationUnreadCountDto
        {
            UnreadCount = await _notificationRepository.CountUnreadByUserIdAsync(userId)
        });
    }

    public async Task<ApiResponse<NotificationDto>> MarkAsReadAsync(Guid userId, string notificationId)
    {
        if (!Guid.TryParse(notificationId, out Guid parsedNotificationId))
        {
            return ApiResponse<NotificationDto>.BadRequest("Mã thông báo không hợp lệ.");
        }

        Domain.Entities.Notification? notification = await _notificationRepository.GetByIdAsync(parsedNotificationId);
        if (notification == null || notification.UserId != userId)
        {
            return ApiResponse<NotificationDto>.NotFound("Không tìm thấy thông báo.");
        }

        if (notification.IsRead != true)
        {
            await _notificationRepository.MarkAsReadAsync(parsedNotificationId);
            notification.IsRead = true;
        }

        return ApiResponse<NotificationDto>.Ok(MapNotification(notification), "Đã đánh dấu đã đọc.");
    }

    public async Task<ApiResponse<NotificationUnreadCountDto>> MarkAllAsReadAsync(Guid userId)
    {
        await _notificationRepository.MarkAllAsReadAsync(userId);

        return ApiResponse<NotificationUnreadCountDto>.Ok(new NotificationUnreadCountDto
        {
            UnreadCount = 0
        }, "Đã đánh dấu tất cả là đã đọc.");
    }

    private static NotificationDto MapNotification(Domain.Entities.Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Title = notification.Title ?? string.Empty,
            Content = notification.Content ?? string.Empty,
            IsRead = notification.IsRead == true,
            CreatedAt = notification.CreatedAt ?? DateTime.UtcNow
        };
    }
}
