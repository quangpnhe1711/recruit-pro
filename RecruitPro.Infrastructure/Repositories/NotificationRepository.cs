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

    public Task<int> CountUnreadByUserIdAsync(Guid userId)
    {
        return _context.Notifications
            .AsNoTracking()
            .CountAsync(notification => notification.UserId == userId && notification.IsRead != true);
    }
}
