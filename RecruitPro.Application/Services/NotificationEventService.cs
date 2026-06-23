using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Services;

public class NotificationEventService : INotificationEventService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationEventService(
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task PublishNewApplicationReceivedAsync(RecruitPro.Domain.Entities.Application application)
    {
        string candidateName = application.User.FullName;
        string jobTitle = application.Job.Title;

        await PublishToUsersAsync(
            [application.Job.CreatedBy],
            $"Ứng viên mới ứng tuyển vào {jobTitle}",
            $"{candidateName} vừa ứng tuyển vào vị trí {jobTitle}.");
    }

    public async Task PublishApplicationStatusChangedAsync(RecruitPro.Domain.Entities.Application application, ApplicationStatus previousStatus)
    {
        if (application.Status == previousStatus)
        {
            return;
        }

        string jobTitle = application.Job.Title;
        string statusLabel = BuildApplicationStatusLabel(application.Status);

        await PublishToUsersAsync(
            [application.UserId],
            "Trạng thái hồ sơ đã được cập nhật",
            $"Hồ sơ của bạn cho vị trí {jobTitle} đã chuyển sang trạng thái {statusLabel}.");
    }

    public async Task PublishInterviewScheduledAsync(RecruitPro.Domain.Entities.Application application, Interview interview, Guid? interviewerId)
    {
        string jobTitle = application.Job.Title;
        string scheduledAt = interview.InterviewDate.ToString("HH:mm 'ngày' dd/MM/yyyy");
        HashSet<Guid> recipients = [application.UserId];

        if (interviewerId.HasValue)
        {
            recipients.Add(interviewerId.Value);
        }

        await PublishToUsersAsync(
            recipients,
            "Bạn có lịch phỏng vấn mới",
            $"Lịch phỏng vấn cho vị trí {jobTitle} được đặt vào {scheduledAt}.");
    }

    public async Task PublishCandidateScoreReadyAsync(RecruitPro.Domain.Entities.Application application)
    {
        string candidateName = application.User.FullName;
        string jobTitle = application.Job.Title;

        await PublishToUsersAsync(
            [application.Job.CreatedBy],
            "Điểm đánh giá ứng viên đã sẵn sàng",
            $"Hệ thống đã hoàn tất đánh giá ứng viên {candidateName} cho vị trí {jobTitle}.");
    }

    private async Task PublishToUsersAsync(IEnumerable<Guid> userIds, string title, string content)
    {
        Guid[] recipientIds = userIds
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();

        if (recipientIds.Length == 0)
        {
            return;
        }

        List<Notification> notifications = [];
        foreach (Guid recipientId in recipientIds)
        {
            User? user = await _userRepository.GetByIdAsync(recipientId);
            if (user == null)
            {
                continue;
            }

            notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = recipientId,
                Title = title,
                Content = content,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (notifications.Count == 0)
        {
            return;
        }

        await _notificationRepository.AddRangeAsync(notifications);
        await _unitOfWork.SaveChangesAsync();
    }

    private static string BuildApplicationStatusLabel(ApplicationStatus status)
    {
        return status switch
        {
            ApplicationStatus.Applied => "Đã ứng tuyển",
            ApplicationStatus.Screening => "Sàng lọc",
            ApplicationStatus.ManagerReview => "Quản lý đánh giá",
            ApplicationStatus.Interview => "Phỏng vấn",
            ApplicationStatus.Offer => "Offer",
            ApplicationStatus.Hired => "Đã nhận việc",
            ApplicationStatus.Rejected => "Từ chối",
            ApplicationStatus.OfferDeclined => "Đã từ chối offer",
            _ => status.ToString()
        };
    }
}
