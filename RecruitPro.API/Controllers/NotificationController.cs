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

    /// <summary>Returns { unseen, unread } counts for the bell badge and item styling.</summary>
    [HttpGet("api/notifications/counts")]
    public async Task<IActionResult> GetCounts()
    {
        var result = await _notificationService.GetCountsAsync(User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Legacy endpoint kept for compatibility. Prefer /counts which returns both unseen and unread.</summary>
    [HttpGet("api/notifications/unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var result = await _notificationService.GetCountsAsync(User.GetCurrentUserId());
        // Return in the old shape so existing callers still work.
        if (!result.Success || result.Data is null)
            return StatusCode(result.StatusCode, result);
        return Ok(new { success = true, statusCode = 200, data = new { unreadCount = result.Data.Unread } });
    }

    /// <summary>Opens the bell: mark all notifications as SEEN (not read).</summary>
    [HttpPost("api/notifications/seen")]
    public async Task<IActionResult> MarkAllAsSeen()
    {
        var result = await _notificationService.MarkAllAsSeenAsync(User.GetCurrentUserId());
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
