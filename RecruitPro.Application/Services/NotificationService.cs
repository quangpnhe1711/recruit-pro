using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using System.Text.Json;

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

    public async Task<ApiResponse<NotificationCountsDto>> GetCountsAsync(Guid userId)
    {
        int unseen = await _notificationRepository.CountUnseenByUserIdAsync(userId);
        int unread = await _notificationRepository.CountUnreadByUserIdAsync(userId);
        return ApiResponse<NotificationCountsDto>.Ok(new NotificationCountsDto
        {
            Unseen = unseen,
            Unread = unread
        });
    }

    public async Task<ApiResponse<NotificationDto>> MarkAsReadAsync(Guid userId, string notificationId)
    {
        if (!Guid.TryParse(notificationId, out Guid parsedNotificationId))
        {
            return ApiResponse<NotificationDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        Domain.Entities.Notification? notification = await _notificationRepository.GetByIdAsync(parsedNotificationId);
        if (notification == null || notification.UserId != userId)
        {
            return ApiResponse<NotificationDto>.NotFound(ErrorCodes.NotificationNotFound);
        }

        if (notification.IsRead != true)
        {
            DateTime now = DbDateTime.Now;
            await _notificationRepository.MarkAsReadAsync(parsedNotificationId, now);
            notification.IsRead = true;
            notification.ReadAt = now;
            notification.IsSeen = true;
            notification.SeenAt ??= now;
        }

        return ApiResponse<NotificationDto>.Ok(MapNotification(notification), "Đã đánh dấu đã đọc.");
    }

    public async Task<ApiResponse<NotificationCountsDto>> MarkAllAsSeenAsync(Guid userId)
    {
        DateTime now = DbDateTime.Now;
        await _notificationRepository.MarkAllAsSeenAsync(userId, now);

        int unread = await _notificationRepository.CountUnreadByUserIdAsync(userId);
        return ApiResponse<NotificationCountsDto>.Ok(new NotificationCountsDto
        {
            Unseen = 0,
            Unread = unread
        }, "Đã đánh dấu tất cả là đã xem.");
    }

    public async Task<ApiResponse<NotificationCountsDto>> MarkAllAsReadAsync(Guid userId)
    {
        await _notificationRepository.MarkAllAsReadAsync(userId);

        return ApiResponse<NotificationCountsDto>.Ok(new NotificationCountsDto
        {
            Unseen = 0,
            Unread = 0
        }, "Đã đánh dấu tất cả là đã đọc.");
    }

    private static NotificationDto MapNotification(Domain.Entities.Notification notification)
    {
        object? data = null;
        if (!string.IsNullOrWhiteSpace(notification.DataJson))
        {
            try
            {
                data = JsonSerializer.Deserialize<JsonElement>(notification.DataJson);
            }
            catch
            {
                data = notification.DataJson;
            }
        }

        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            EventCode = notification.EventCode ?? string.Empty,
            Title = notification.Title ?? string.Empty,
            Body = notification.Body ?? notification.Content ?? string.Empty,
            Type = notification.Type ?? string.Empty,
            Data = data,
            EntityType = notification.EntityType,
            EntityId = notification.EntityId,
            IsRead = notification.IsRead == true,
            IsSeen = notification.IsSeen == true,
            CreatedAt = notification.CreatedAt ?? DateTime.UtcNow
        };
    }
}
