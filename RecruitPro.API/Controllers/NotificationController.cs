using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.API.Extensions;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
[Authorize]
public class NotificationController : ControllerBase
{
    // SSE serialization must match the GET /api/notifications envelope so the frontend parses one
    // notification shape regardless of transport (camelCase, default web options).
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

    // Comment heartbeat keeps the connection (and any intermediary proxy) from idling out. 25s sits
    // safely under common 30–60s proxy read timeouts.
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(25);

    private readonly INotificationService _notificationService;
    private readonly INotificationSseBroker _sseBroker;

    public NotificationController(INotificationService notificationService, INotificationSseBroker sseBroker)
    {
        _notificationService = notificationService;
        _sseBroker = sseBroker;
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

    /// <summary>
    /// Authenticated, user-scoped Server-Sent Events stream of newly created notifications. Replaces
    /// the former SignalR hub. The stream stays open until the client disconnects (request aborted),
    /// emitting <c>notification.created</c> events plus periodic <c>: ping</c> heartbeat comments.
    ///
    /// Best-effort: the persisted notification row is the source of truth. The frontend re-syncs the
    /// list/counts from REST on connect/reconnect, so anything missed while disconnected is recovered.
    /// </summary>
    [HttpGet("api/notifications/stream")]
    public async Task StreamNotifications(CancellationToken cancellationToken)
    {
        Guid userId = User.GetCurrentUserId();

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        // Defeat reverse-proxy response buffering (Nginx honours X-Accel-Buffering: no) so events are
        // delivered as they happen instead of being held back.
        Response.Headers["X-Accel-Buffering"] = "no";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        using NotificationSseSubscription subscription = _sseBroker.Subscribe(userId);
        ChannelReader<NotificationDto> reader = subscription.Reader;

        // Open the stream immediately so the client (and any proxy) sees headers + first bytes.
        await WriteCommentAsync(": connected", cancellationToken);

        using var heartbeat = new PeriodicTimer(HeartbeatInterval);
        Task<bool> heartbeatTick = heartbeat.WaitForNextTickAsync(cancellationToken).AsTask();
        Task<bool> dataReady = reader.WaitToReadAsync(cancellationToken).AsTask();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Task completed = await Task.WhenAny(heartbeatTick, dataReady);

                if (completed == heartbeatTick)
                {
                    if (!await heartbeatTick)
                    {
                        break;
                    }

                    await WriteCommentAsync(": ping", cancellationToken);
                    heartbeatTick = heartbeat.WaitForNextTickAsync(cancellationToken).AsTask();
                }
                else
                {
                    if (!await dataReady)
                    {
                        break; // channel completed (subscription removed)
                    }

                    while (reader.TryRead(out NotificationDto? notification))
                    {
                        await WriteEventAsync("notification.created", notification, cancellationToken);
                    }

                    dataReady = reader.WaitToReadAsync(cancellationToken).AsTask();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected (request aborted) — clean, expected shutdown. The subscription is
            // released by the using block above.
        }
    }

    private async Task WriteEventAsync(string eventName, NotificationDto notification, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(notification, SseJsonOptions);
        await Response.WriteAsync($"event: {eventName}\n", cancellationToken);
        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private async Task WriteCommentAsync(string comment, CancellationToken cancellationToken)
    {
        await Response.WriteAsync($"{comment}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
