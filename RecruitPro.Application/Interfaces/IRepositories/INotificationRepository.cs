namespace RecruitPro.Application.Interfaces.IRepositories;

public interface INotificationRepository
{
    Task<int> CountUnreadByUserIdAsync(Guid userId);
}
