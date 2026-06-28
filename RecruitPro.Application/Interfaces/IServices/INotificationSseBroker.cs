using System.Threading.Channels;
using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

/// <summary>
/// In-memory, user-scoped fan-out for Server-Sent Events (SSE) notification delivery. Replaces the
/// former SignalR hub: notifications only need one-way server-to-client realtime, so SSE avoids the
/// negotiate/WebSocket/proxy complexity that broke <c>/hubs/notifications</c> in production (405 on
/// the negotiate POST behind the reverse proxy).
///
/// Delivery is best-effort: the notification row in the database is the source of truth. If a client
/// misses a pushed event (full channel, transient disconnect) the frontend re-syncs from the REST
/// list/counts endpoints on (re)connect.
/// </summary>
public interface INotificationSseBroker
{
    /// <summary>
    /// Opens a new subscription for <paramref name="userId"/>. A single user may hold many concurrent
    /// subscriptions (one per open browser tab). Dispose the returned subscription to detach the tab
    /// and release its channel.
    /// </summary>
    NotificationSseSubscription Subscribe(Guid userId);

    /// <summary>
    /// Pushes <paramref name="notification"/> to every active subscription owned by
    /// <paramref name="userId"/> — and to nobody else. Never blocks the caller's business transaction:
    /// writes are non-blocking and a full channel drops the oldest event rather than waiting.
    /// </summary>
    Task PublishAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// A single SSE subscription (one browser tab). Holds the bounded channel reader the endpoint drains
/// and unregisters itself from the broker when disposed.
/// </summary>
public sealed class NotificationSseSubscription : IDisposable
{
    private readonly Action _onDispose;
    private int _disposed;

    public NotificationSseSubscription(ChannelReader<NotificationDto> reader, Action onDispose)
    {
        Reader = reader;
        _onDispose = onDispose;
    }

    /// <summary>The bounded channel reader the SSE endpoint awaits for new notifications.</summary>
    public ChannelReader<NotificationDto> Reader { get; }

    public void Dispose()
    {
        // Idempotent: the endpoint disposes in a finally block; guard against double-removal.
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _onDispose();
        }
    }
}
