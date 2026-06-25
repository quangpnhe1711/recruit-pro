using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.API.Extensions;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet("api/notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _notificationService.GetUserNotificationsAsync(User.GetCurrentUserId(), page, pageSize);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/notifications/unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var result = await _notificationService.GetUnreadCountAsync(User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/notifications/{notificationId}/read")]
    [HttpPost("api/notifications/{notificationId}/read")]
    public async Task<IActionResult> MarkAsRead(string notificationId)
    {
        var result = await _notificationService.MarkAsReadAsync(User.GetCurrentUserId(), notificationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/notifications/read-all")]
    [HttpPost("api/notifications/read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var result = await _notificationService.MarkAllAsReadAsync(User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }
}
