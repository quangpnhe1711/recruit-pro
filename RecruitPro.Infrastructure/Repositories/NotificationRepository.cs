using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _context;

    public NotificationRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(Domain.Entities.Notification notification)
    {
        return _context.Notifications.AddAsync(notification).AsTask();
    }

    public Task AddRangeAsync(IEnumerable<Domain.Entities.Notification> notifications)
    {
        return _context.Notifications.AddRangeAsync(notifications);
    }

    public async Task<IReadOnlyList<Domain.Entities.Notification>> GetByUserIdAsync(Guid userId, int page, int pageSize)
    {
        return await _context.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> CountByUserIdAsync(Guid userId)
    {
        return _context.Notifications
            .AsNoTracking()
            .CountAsync(notification => notification.UserId == userId);
    }

    public Task<int> CountUnreadByUserIdAsync(Guid userId)
    {
        return _context.Notifications
            .AsNoTracking()
            .CountAsync(notification => notification.UserId == userId && notification.IsRead != true);
    }

    public Task<Domain.Entities.Notification?> GetByIdAsync(Guid notificationId)
    {
        return _context.Notifications
            .FirstOrDefaultAsync(notification => notification.Id == notificationId);
    }

    public Task<int> CountUnseenByUserIdAsync(Guid userId)
    {
        return _context.Notifications
            .AsNoTracking()
            .CountAsync(notification => notification.UserId == userId && notification.IsSeen != true);
    }

    public async Task MarkAsReadAsync(Guid notificationId, DateTime readAt)
    {
        await _context.Notifications
            .Where(notification => notification.Id == notificationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(notification => notification.IsRead, true)
                .SetProperty(notification => notification.ReadAt, readAt)
                .SetProperty(notification => notification.IsSeen, true)
                .SetProperty(notification => notification.SeenAt, (DateTime?)readAt));
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId)
    {
        return await _context.Notifications
            .Where(notification => notification.UserId == userId && notification.IsRead != true)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(notification => notification.IsRead, true));
    }

    public async Task<int> MarkAllAsSeenAsync(Guid userId, DateTime seenAt)
    {
        return await _context.Notifications
            .Where(notification => notification.UserId == userId && notification.IsSeen != true)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(notification => notification.IsSeen, true)
                .SetProperty(notification => notification.SeenAt, (DateTime?)seenAt));
    }
}
