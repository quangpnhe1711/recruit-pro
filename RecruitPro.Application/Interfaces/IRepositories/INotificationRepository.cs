namespace RecruitPro.Application.Interfaces.IRepositories;

public interface INotificationRepository
{
    Task AddAsync(Domain.Entities.Notification notification);
    Task AddRangeAsync(IEnumerable<Domain.Entities.Notification> notifications);
    Task<IReadOnlyList<Domain.Entities.Notification>> GetByUserIdAsync(Guid userId, int page, int pageSize);
    Task<int> CountByUserIdAsync(Guid userId);
    Task<int> CountUnreadByUserIdAsync(Guid userId);
    Task<Domain.Entities.Notification?> GetByIdAsync(Guid notificationId);
    Task MarkAsReadAsync(Guid notificationId);
    Task<int> MarkAllAsReadAsync(Guid userId);
}
