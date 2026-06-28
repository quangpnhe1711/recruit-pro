using System.Collections.Concurrent;
using System.Threading.Channels;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Realtime;

/// <summary>
/// Process-local <see cref="INotificationSseBroker"/>. Registered as a singleton so every request and
/// the notification publisher share one fan-out table. Keyed by userId; each user maps to one channel
/// per open tab. Bounded + drop-oldest so a stalled/slow client can never grow memory without bound or
/// stall the publisher.
///
/// Single-process only — fine for the current single-instance deployment. A multi-instance deployment
/// would need a backplane (Redis pub/sub), but the DB remains the source of truth so a missed push is
/// recovered by the frontend's REST re-sync.
/// </summary>
public sealed class InMemoryNotificationSseBroker : INotificationSseBroker
{
    // Capacity per tab. ~64 unread bursts is far more than a human bell ever shows; beyond it we drop
    // the oldest queued event (the client re-syncs counts/list from REST on its next interaction).
    private const int ChannelCapacity = 64;

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<NotificationDto>>> _subscribers = new();
    private readonly ILogger<InMemoryNotificationSseBroker> _logger;

    public InMemoryNotificationSseBroker(ILogger<InMemoryNotificationSseBroker> logger)
    {
        _logger = logger;
    }

    public NotificationSseSubscription Subscribe(Guid userId)
    {
        Channel<NotificationDto> channel = Channel.CreateBounded<NotificationDto>(
            new BoundedChannelOptions(ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });

        Guid subscriptionId = Guid.NewGuid();
        ConcurrentDictionary<Guid, Channel<NotificationDto>> tabs =
            _subscribers.GetOrAdd(userId, static _ => new ConcurrentDictionary<Guid, Channel<NotificationDto>>());
        tabs[subscriptionId] = channel;

        return new NotificationSseSubscription(channel.Reader, () => Remove(userId, subscriptionId));
    }

    public Task PublishAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        // Publish ONLY to the target user's channels — never broadcast. A user with no open tab simply
        // has no channel; the notification is already persisted and will appear on next REST load.
        if (_subscribers.TryGetValue(userId, out ConcurrentDictionary<Guid, Channel<NotificationDto>>? tabs))
        {
            foreach (Channel<NotificationDto> channel in tabs.Values)
            {
                // Non-blocking. With DropOldest, TryWrite essentially always succeeds; the guard only
                // trips on a completed channel, which we then log without throwing.
                if (!channel.Writer.TryWrite(notification))
                {
                    _logger.LogWarning(
                        "Dropped SSE notification {NotificationId} for user {UserId}: channel full or closed.",
                        notification.Id, userId);
                }
            }
        }

        return Task.CompletedTask;
    }

    private void Remove(Guid userId, Guid subscriptionId)
    {
        if (_subscribers.TryGetValue(userId, out ConcurrentDictionary<Guid, Channel<NotificationDto>>? tabs))
        {
            if (tabs.TryRemove(subscriptionId, out Channel<NotificationDto>? channel))
            {
                channel.Writer.TryComplete();
            }

            // Prune the user entry once their last tab closes to keep the table small.
            if (tabs.IsEmpty)
            {
                _subscribers.TryRemove(userId, out _);
            }
        }
    }
}
