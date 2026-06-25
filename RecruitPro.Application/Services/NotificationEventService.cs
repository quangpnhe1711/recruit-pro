using RecruitPro.Application.Interfaces;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using System.Text.Json;

namespace RecruitPro.Application.Services;

public class NotificationEventService : INotificationEventService
{
    private const string HeadDepartmentRoleName = "HeadDepartment";
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationRealtimeSender _notificationRealtimeSender;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationEventService(
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        INotificationRealtimeSender notificationRealtimeSender,
        IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _notificationRealtimeSender = notificationRealtimeSender;
        _unitOfWork = unitOfWork;
    }

    public async Task PublishNewApplicationReceivedAsync(RecruitPro.Domain.Entities.Application application)
    {
        string candidateName = application.User.FullName;
        string jobTitle = application.Job.Title;

        await PublishToUsersAsync(
            [application.Job.CreatedBy],
            "new_application_received",
            $"Ứng viên mới ứng tuyển vào {jobTitle}",
            $"{candidateName} vừa ứng tuyển vào vị trí {jobTitle}.",
            "APPLICATION",
            "application",
            application.Id,
            new Dictionary<string, object?>
            {
                ["candidateName"] = candidateName,
                ["jobTitle"] = jobTitle,
                ["jobId"] = application.JobId,
                ["applicationId"] = application.Id
            });
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
            "application_status_changed",
            "Trạng thái hồ sơ đã được cập nhật",
            $"Hồ sơ của bạn cho vị trí {jobTitle} đã chuyển sang trạng thái {statusLabel}.",
            "APPLICATION",
            "application",
            application.Id,
            new Dictionary<string, object?>
            {
                ["jobTitle"] = jobTitle,
                ["newStatus"] = statusLabel,
                ["applicationId"] = application.Id
            });
    }

    public async Task PublishInterviewScheduledAsync(RecruitPro.Domain.Entities.Application application, Interview interview, Guid? interviewerId)
    {
        string jobTitle = application.Job.Title;
        string scheduledAt = interview.InterviewDate.ToString("HH:mm 'ngày' dd/MM/yyyy");
        HashSet<Guid> recipients = [application.UserId];
        IReadOnlyList<User> headDepartments = await _userRepository.GetUsersInRolesAsync(HeadDepartmentRoleName);
        foreach (User headDepartment in headDepartments)
        {
            recipients.Add(headDepartment.Id);
        }

        if (interviewerId.HasValue)
        {
            recipients.Add(interviewerId.Value);
        }

        await PublishToUsersAsync(
            recipients,
            "interview_scheduled",
            "Bạn có lịch phỏng vấn mới",
            $"Lịch phỏng vấn cho vị trí {jobTitle} được đặt vào {scheduledAt}.",
            "INTERVIEW",
            "interview",
            interview.Id,
            new Dictionary<string, object?>
            {
                ["jobTitle"] = jobTitle,
                ["scheduledAt"] = interview.InterviewDate,
                ["applicationId"] = application.Id,
                ["interviewId"] = interview.Id
            });
    }

    public async Task PublishCandidateScoreReadyAsync(RecruitPro.Domain.Entities.Application application)
    {
        string candidateName = application.User.FullName;
        string jobTitle = application.Job.Title;

        await PublishToUsersAsync(
            [application.Job.CreatedBy],
            "candidate_score_ready",
            "Điểm đánh giá ứng viên đã sẵn sàng",
            $"Hệ thống đã hoàn tất đánh giá ứng viên {candidateName} cho vị trí {jobTitle}.",
            "APPLICATION",
            "application",
            application.Id,
            new Dictionary<string, object?>
            {
                ["candidateName"] = candidateName,
                ["jobTitle"] = jobTitle,
                ["applicationId"] = application.Id,
                ["finalScore"] = application.FinalScore
            });
    }

    private async Task PublishToUsersAsync(
        IEnumerable<Guid> userIds,
        string eventCode,
        string title,
        string body,
        string type,
        string? entityType,
        Guid? entityId,
        Dictionary<string, object?> data)
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
                EventCode = eventCode,
                Title = title,
                Body = body,
                Content = body,
                DataJson = JsonSerializer.Serialize(data),
                EntityType = entityType,
                EntityId = entityId,
                Type = type,
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

        foreach (Notification notification in notifications)
        {
            await _notificationRealtimeSender.SendToUserAsync(
                notification.UserId,
                new NotificationDto
                {
                    Id = notification.Id,
                    UserId = notification.UserId,
                    EventCode = notification.EventCode ?? string.Empty,
                    Title = notification.Title ?? string.Empty,
                    Body = notification.Body ?? string.Empty,
                    Type = notification.Type ?? string.Empty,
                    Data = data,
                    EntityType = notification.EntityType,
                    EntityId = notification.EntityId,
                    IsRead = false,
                    CreatedAt = notification.CreatedAt ?? DateTime.UtcNow
                });
        }
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
            ApplicationStatus.Withdrawn => "Đã rút đơn",
            _ => status.ToString()
        };
    }
}
