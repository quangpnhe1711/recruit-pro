using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Notifications;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using System.Text.Json;

namespace RecruitPro.Application.Services;

/// <summary>
/// Ownership-routed, click-ready notification publisher (NOTIFICATION-EVENT-MATRIX.md). Recipients come
/// from the application/job ownership snapshot (<see cref="ApplicationOwnershipResolver"/>), never broad
/// role broadcast; recipients are deduplicated; and every notification carries a role-aware frontend
/// deep link. Publishing is best-effort and post-commit — callers wrap each call so a failure here does
/// not fail the committed business action.
/// </summary>
public class NotificationEventService : INotificationEventService
{
    private const string CandidateFallbackName = "Ứng viên";
    private const string ActorFallbackName = "Người dùng";
    private const string JobFallbackTitle = "vị trí tuyển dụng";

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

    // ============================ Job workflow ============================

    public async Task PublishJobSubmittedForApprovalAsync(Job job, Guid actorUserId)
    {
        // Recipient = the department head who must approve. When no head is configured there is nobody
        // to route to (the submission itself is gated elsewhere by DEPARTMENT_HEAD_REQUIRED).
        Guid? headId = job.Department?.HeadUserId;
        if (headId is null)
        {
            return;
        }

        string actorName = await ResolveUserNameAsync(actorUserId, ActorFallbackName);
        NotificationTemplates.Message message = NotificationTemplates.JobSubmittedForApproval(actorName, job.Title);

        await PublishAsync(
            NotificationEventCodes.JobSubmittedForApproval,
            NotificationTypeBuckets.Job,
            message,
            BuildJobData(job, actorUserId),
            [new NotificationRecipient(headId.Value, NotificationLinks.HeadJobApproval(job.Id), NotificationTargetTypes.JobApproval, job.Id)]);
    }

    public async Task PublishJobApprovedAsync(Job job)
    {
        Guid recruiterId = job.RecruiterId ?? job.CreatedBy;
        NotificationTemplates.Message message = NotificationTemplates.JobApproved(job.Title);

        await PublishAsync(
            NotificationEventCodes.JobApproved,
            NotificationTypeBuckets.Job,
            message,
            BuildJobData(job, job.ApprovedBy),
            [new NotificationRecipient(recruiterId, NotificationLinks.HrJob(job.Id), NotificationTargetTypes.Job, job.Id)]);
    }

    public async Task PublishJobRejectedAsync(Job job, string? reason = null)
    {
        Guid recruiterId = job.RecruiterId ?? job.CreatedBy;
        NotificationTemplates.Message message = NotificationTemplates.JobRejected(job.Title, reason);
        Dictionary<string, object?> data = BuildJobData(job, job.ApprovedBy);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            data["reason"] = reason;
        }

        await PublishAsync(
            NotificationEventCodes.JobRejected,
            NotificationTypeBuckets.Job,
            message,
            data,
            [new NotificationRecipient(recruiterId, NotificationLinks.HrJob(job.Id), NotificationTargetTypes.Job, job.Id)]);
    }

    // ========================= Application workflow =========================

    public async Task PublishApplicationAppliedAsync(RecruitPro.Domain.Entities.Application application)
    {
        // HR-first (BR-OWN-006): route to the assigned recruiter only — NOT the department head.
        ApplicationOwnership ownership = ApplicationOwnershipResolver.Resolve(application);
        if (ownership.RecruiterUserId is not { } recruiterId)
        {
            return;
        }

        NotificationTemplates.Message message = NotificationTemplates.ApplicationApplied(
            CandidateName(application), JobTitle(application));

        await PublishAsync(
            NotificationEventCodes.ApplicationApplied,
            NotificationTypeBuckets.Application,
            message,
            BuildApplicationData(application, ownership),
            [new NotificationRecipient(recruiterId, NotificationLinks.HrApplication(application.Id), NotificationTargetTypes.Application, application.Id)]);
    }

    public async Task PublishScreeningStartedAsync(RecruitPro.Domain.Entities.Application application)
    {
        NotificationTemplates.Message message = NotificationTemplates.ApplicationScreeningStarted(JobTitle(application));

        await PublishAsync(
            NotificationEventCodes.ApplicationScreeningStarted,
            NotificationTypeBuckets.Application,
            message,
            BuildApplicationData(application, ApplicationOwnershipResolver.Resolve(application)),
            [new NotificationRecipient(application.UserId, NotificationLinks.CandidateApplication(application.Id), NotificationTargetTypes.Application, application.Id)]);
    }

    public async Task PublishDepartmentHeadReviewRequestedAsync(RecruitPro.Domain.Entities.Application application, Guid? actorUserId)
    {
        ApplicationOwnership ownership = ApplicationOwnershipResolver.Resolve(application);
        if (ownership.DepartmentHeadUserId is not { } headId)
        {
            return;
        }

        NotificationTemplates.Message message = NotificationTemplates.DepartmentHeadReviewRequested(
            CandidateName(application), JobTitle(application));

        Dictionary<string, object?> data = BuildApplicationData(application, ownership);
        data["candidateUserId"] = application.UserId;
        data["departmentHeadReviewRequestedAt"] = application.DepartmentHeadReviewRequestedAt;
        data["actorUserId"] = actorUserId;

        await PublishAsync(
            NotificationEventCodes.ApplicationDepartmentHeadReviewRequested,
            NotificationTypeBuckets.Application,
            message,
            data,
            [new NotificationRecipient(headId, NotificationLinks.HeadApplicationReview(application.Id), NotificationTargetTypes.ApplicationReview, application.Id)]);
    }

    public async Task PublishInterviewRequestedAsync(RecruitPro.Domain.Entities.Application application)
    {
        // DepartmentHead approved for interview; HR/recruiter must now schedule it.
        ApplicationOwnership ownership = ApplicationOwnershipResolver.Resolve(application);
        if (ownership.RecruiterUserId is not { } recruiterId)
        {
            return;
        }

        NotificationTemplates.Message message = NotificationTemplates.InterviewRequested(
            CandidateName(application), JobTitle(application));

        await PublishAsync(
            NotificationEventCodes.ApplicationInterviewRequested,
            NotificationTypeBuckets.Application,
            message,
            BuildApplicationData(application, ownership),
            [new NotificationRecipient(recruiterId, NotificationLinks.HrInterviewSchedule(application.Id), NotificationTargetTypes.InterviewRequest, application.Id)]);
    }

    public async Task PublishApplicationWithdrawnAsync(RecruitPro.Domain.Entities.Application application, ApplicationStatus statusBeforeWithdraw)
    {
        ApplicationOwnership ownership = ApplicationOwnershipResolver.Resolve(application);
        NotificationTemplates.Message message = NotificationTemplates.ApplicationWithdrawn(
            CandidateName(application), JobTitle(application));

        List<NotificationRecipient> recipients = [];
        if (ownership.RecruiterUserId is { } recruiterId)
        {
            recipients.Add(new NotificationRecipient(recruiterId, NotificationLinks.HrApplication(application.Id), NotificationTargetTypes.Application, application.Id));
        }

        // Notify the department head only when the application had already reached their desk
        // (ManagerReview / Interview / Offer). Earlier-stage withdrawals stay HR-only.
        bool reachedHead = statusBeforeWithdraw
            is ApplicationStatus.ManagerReview
            or ApplicationStatus.Interview
            or ApplicationStatus.Offer;
        if (reachedHead && ownership.DepartmentHeadUserId is { } headId)
        {
            recipients.Add(new NotificationRecipient(headId, NotificationLinks.HeadApplicationReview(application.Id), NotificationTargetTypes.ApplicationReview, application.Id));
        }

        Dictionary<string, object?> data = BuildApplicationData(application, ownership);
        data["oldStatus"] = statusBeforeWithdraw.ToString();
        data["newStatus"] = ApplicationStatus.Withdrawn.ToString();

        await PublishAsync(
            NotificationEventCodes.ApplicationWithdrawn,
            NotificationTypeBuckets.Application,
            message,
            data,
            recipients);
    }

    // ========================== Interview workflow ==========================

    public async Task PublishInterviewScheduledAsync(RecruitPro.Domain.Entities.Application application, Interview interview, Guid? interviewerId)
    {
        ApplicationOwnership ownership = ApplicationOwnershipResolver.Resolve(application);
        string scheduledAt = FormatScheduledAt(interview.InterviewDate);

        // Candidate gets a candidate-safe interview link (never an HR route).
        List<NotificationRecipient> recipients =
        [
            new NotificationRecipient(application.UserId, NotificationLinks.CandidateInterview(interview.Id), NotificationTargetTypes.Interview, interview.Id, application.Id),
        ];
        if (ownership.RecruiterUserId is { } recruiterId)
        {
            recipients.Add(new NotificationRecipient(recruiterId, NotificationLinks.HrInterview(interview.Id), NotificationTargetTypes.Interview, interview.Id, application.Id));
        }
        if (ownership.DepartmentHeadUserId is { } headId)
        {
            recipients.Add(new NotificationRecipient(headId, NotificationLinks.HeadApplicationReview(application.Id), NotificationTargetTypes.Interview, interview.Id, application.Id));
        }
        if (interviewerId is { } interviewer)
        {
            recipients.Add(new NotificationRecipient(interviewer, NotificationLinks.HrInterview(interview.Id), NotificationTargetTypes.Interview, interview.Id, application.Id));
        }

        Dictionary<string, object?> data = BuildApplicationData(application, ownership);
        data["interviewId"] = interview.Id;
        data["scheduledAt"] = interview.InterviewDate;
        data["meetingLink"] = interview.MeetingLink;
        data["location"] = interview.Location;
        if (interviewerId.HasValue)
        {
            data["interviewerId"] = interviewerId.Value;
        }

        // The candidate sees a candidate-phrased body; internal owners see the candidate name.
        NotificationTemplates.Message internalMessage = NotificationTemplates.InterviewScheduled(CandidateName(application), JobTitle(application));
        NotificationTemplates.Message candidateMessage = NotificationTemplates.InterviewScheduledForCandidate(JobTitle(application), scheduledAt);

        await PublishAsync(
            NotificationEventCodes.InterviewScheduled,
            NotificationTypeBuckets.Interview,
            internalMessage,
            data,
            recipients,
            candidateUserId: application.UserId,
            candidateMessage: candidateMessage);
    }

    public async Task PublishInterviewCompletedAsync(RecruitPro.Domain.Entities.Application application, Interview interview)
    {
        ApplicationOwnership ownership = ApplicationOwnershipResolver.Resolve(application);
        NotificationTemplates.Message message = NotificationTemplates.InterviewCompleted(
            CandidateName(application), JobTitle(application));

        List<NotificationRecipient> recipients = [];
        if (ownership.RecruiterUserId is { } recruiterId)
        {
            recipients.Add(new NotificationRecipient(recruiterId, NotificationLinks.HrApplication(application.Id), NotificationTargetTypes.Application, application.Id, interview.Id));
        }
        if (ownership.DepartmentHeadUserId is { } headId)
        {
            recipients.Add(new NotificationRecipient(headId, NotificationLinks.HeadApplicationReview(application.Id), NotificationTargetTypes.ApplicationReview, application.Id, interview.Id));
        }

        Dictionary<string, object?> data = BuildApplicationData(application, ownership);
        data["interviewId"] = interview.Id;

        await PublishAsync(
            NotificationEventCodes.InterviewCompleted,
            NotificationTypeBuckets.Interview,
            message,
            data,
            recipients);
    }

    // ===================== Offer / rejection workflow =====================

    public async Task PublishOfferEmailSentAsync(RecruitPro.Domain.Entities.Application application, ApplicationOffer offer)
    {
        ApplicationOwnership ownership = ApplicationOwnershipResolver.Resolve(application);

        List<NotificationRecipient> recipients =
        [
            // Candidate already receives the email; the in-app record is candidate-safe.
            new NotificationRecipient(application.UserId, NotificationLinks.CandidateOffer(application.Id), NotificationTargetTypes.Offer, offer.Id, application.Id),
        ];
        if (ownership.RecruiterUserId is { } recruiterId)
        {
            recipients.Add(new NotificationRecipient(recruiterId, NotificationLinks.HrApplication(application.Id), NotificationTargetTypes.Offer, offer.Id, application.Id));
        }
        if (ownership.DepartmentHeadUserId is { } headId)
        {
            recipients.Add(new NotificationRecipient(headId, NotificationLinks.HeadApplicationReview(application.Id), NotificationTargetTypes.Offer, offer.Id, application.Id));
        }

        Dictionary<string, object?> data = BuildApplicationData(application, ownership);
        data["offerId"] = offer.Id;
        data["newStatus"] = ApplicationStatus.Offer.ToString();

        NotificationTemplates.Message internalMessage = NotificationTemplates.OfferEmailSent(CandidateName(application), JobTitle(application));
        NotificationTemplates.Message candidateMessage = NotificationTemplates.OfferEmailSentForCandidate(JobTitle(application));

        await PublishAsync(
            NotificationEventCodes.OfferEmailSent,
            NotificationTypeBuckets.Offer,
            internalMessage,
            data,
            recipients,
            candidateUserId: application.UserId,
            candidateMessage: candidateMessage);
    }

    public async Task PublishRejectionEmailSentAsync(RecruitPro.Domain.Entities.Application application)
    {
        ApplicationOwnership ownership = ApplicationOwnershipResolver.Resolve(application);

        List<NotificationRecipient> recipients =
        [
            new NotificationRecipient(application.UserId, NotificationLinks.CandidateApplication(application.Id), NotificationTargetTypes.Application, application.Id),
        ];
        if (ownership.RecruiterUserId is { } recruiterId)
        {
            recipients.Add(new NotificationRecipient(recruiterId, NotificationLinks.HrApplication(application.Id), NotificationTargetTypes.Application, application.Id));
        }
        if (ownership.DepartmentHeadUserId is { } headId)
        {
            recipients.Add(new NotificationRecipient(headId, NotificationLinks.HeadApplicationReview(application.Id), NotificationTargetTypes.ApplicationReview, application.Id));
        }

        Dictionary<string, object?> data = BuildApplicationData(application, ownership);
        data["newStatus"] = ApplicationStatus.Rejected.ToString();

        NotificationTemplates.Message internalMessage = NotificationTemplates.RejectionEmailSent(CandidateName(application), JobTitle(application));
        NotificationTemplates.Message candidateMessage = NotificationTemplates.RejectionEmailSentForCandidate(JobTitle(application));

        await PublishAsync(
            NotificationEventCodes.RejectionEmailSent,
            NotificationTypeBuckets.Application,
            internalMessage,
            data,
            recipients,
            candidateUserId: application.UserId,
            candidateMessage: candidateMessage);
    }

    public async Task PublishOfferAcceptedAsync(RecruitPro.Domain.Entities.Application application, ApplicationOffer? offer)
    {
        NotificationTemplates.Message message = NotificationTemplates.OfferAccepted(
            CandidateName(application), JobTitle(application));
        await PublishOfferOutcomeAsync(application, offer, NotificationEventCodes.OfferAccepted, ApplicationStatus.Hired, message);
    }

    public async Task PublishOfferDeclinedAsync(RecruitPro.Domain.Entities.Application application, ApplicationOffer? offer)
    {
        NotificationTemplates.Message message = NotificationTemplates.OfferDeclined(
            CandidateName(application), JobTitle(application));
        await PublishOfferOutcomeAsync(application, offer, NotificationEventCodes.OfferDeclined, ApplicationStatus.OfferDeclined, message);
    }

    /// <summary>
    /// Shared internal-owner routing for an offer outcome (accepted/declined and, by composition, the
    /// candidate_hired close-out which we fold into offer_accepted — Option A). Recipients are the
    /// recruiter and department head; the candidate is not notified of their own response.
    /// </summary>
    private async Task PublishOfferOutcomeAsync(
        RecruitPro.Domain.Entities.Application application,
        ApplicationOffer? offer,
        string eventCode,
        ApplicationStatus newStatus,
        NotificationTemplates.Message message)
    {
        ApplicationOwnership ownership = ApplicationOwnershipResolver.Resolve(application);
        Guid targetId = offer?.Id ?? application.Id;
        Guid? secondaryTargetId = offer is null ? null : application.Id;

        List<NotificationRecipient> recipients = [];
        if (ownership.RecruiterUserId is { } recruiterId)
        {
            recipients.Add(new NotificationRecipient(recruiterId, NotificationLinks.HrApplication(application.Id), NotificationTargetTypes.Offer, targetId, secondaryTargetId));
        }
        if (ownership.DepartmentHeadUserId is { } headId)
        {
            recipients.Add(new NotificationRecipient(headId, NotificationLinks.HeadApplicationReview(application.Id), NotificationTargetTypes.Offer, targetId, secondaryTargetId));
        }

        Dictionary<string, object?> data = BuildApplicationData(application, ownership);
        data["newStatus"] = newStatus.ToString();
        if (offer is not null)
        {
            data["offerId"] = offer.Id;
        }

        await PublishAsync(eventCode, NotificationTypeBuckets.Offer, message, data, recipients);
    }

    // ============================== Legacy ==============================

    public async Task PublishApplicationStatusChangedAsync(RecruitPro.Domain.Entities.Application application, ApplicationStatus previousStatus)
    {
        if (application.Status == previousStatus)
        {
            return;
        }

        string jobTitle = JobTitle(application);
        string statusLabel = BuildApplicationStatusLabel(application.Status);

        Dictionary<string, object?> data = new()
        {
            ["jobTitle"] = jobTitle,
            ["applicationId"] = application.Id,
            ["oldStatus"] = previousStatus.ToString(),
            ["newStatus"] = application.Status.ToString(),
        };

        await PublishAsync(
            "application_status_changed",
            NotificationTypeBuckets.Application,
            new NotificationTemplates.Message(
                "Trạng thái hồ sơ đã được cập nhật",
                $"Hồ sơ của bạn cho vị trí {jobTitle} đã chuyển sang trạng thái {statusLabel}."),
            data,
            [new NotificationRecipient(application.UserId, NotificationLinks.CandidateApplication(application.Id), NotificationTargetTypes.Application, application.Id)]);
    }

    public async Task PublishCandidateScoreReadyAsync(RecruitPro.Domain.Entities.Application application)
    {
        Guid recruiterId = application.Job.CreatedBy;
        string candidateName = CandidateName(application);
        string jobTitle = JobTitle(application);

        Dictionary<string, object?> data = new()
        {
            ["candidateName"] = candidateName,
            ["jobTitle"] = jobTitle,
            ["applicationId"] = application.Id,
            ["finalScore"] = application.FinalScore,
        };

        await PublishAsync(
            NotificationEventCodes.CandidateScoreReady,
            NotificationTypeBuckets.Application,
            new NotificationTemplates.Message(
                "Điểm đánh giá ứng viên đã sẵn sàng",
                $"Hệ thống đã hoàn tất đánh giá ứng viên {candidateName} cho vị trí {jobTitle}."),
            data,
            [new NotificationRecipient(recruiterId, NotificationLinks.HrApplication(application.Id), NotificationTargetTypes.Application, application.Id)]);
    }

    // ============================== Internals ==============================

    /// <summary>
    /// Persists one notification per (deduplicated) recipient and pushes it over the realtime channel.
    /// Each recipient's stored payload carries the shared event data PLUS that recipient's own role-aware
    /// deep link (url/targetType/targetId/secondaryTargetId/routeHint), so every record is click-ready.
    /// </summary>
    private async Task PublishAsync(
        string eventCode,
        string typeBucket,
        NotificationTemplates.Message message,
        Dictionary<string, object?> sharedData,
        IEnumerable<NotificationRecipient> recipients,
        Guid? candidateUserId = null,
        NotificationTemplates.Message? candidateMessage = null)
    {
        HashSet<Guid> seen = [];
        List<(Notification Notification, Dictionary<string, object?> Data)> prepared = [];

        foreach (NotificationRecipient recipient in recipients)
        {
            if (recipient.UserId == Guid.Empty || !seen.Add(recipient.UserId))
            {
                continue;
            }

            User? user = await _userRepository.GetByIdAsync(recipient.UserId);
            if (user is null)
            {
                continue;
            }

            Dictionary<string, object?> data = new(sharedData)
            {
                ["eventCode"] = eventCode,
                ["url"] = recipient.Url,
                ["targetType"] = recipient.TargetType,
                ["targetId"] = recipient.TargetId,
            };
            if (recipient.SecondaryTargetId.HasValue)
            {
                data["secondaryTargetId"] = recipient.SecondaryTargetId.Value;
            }
            if (!string.IsNullOrWhiteSpace(recipient.RouteHint))
            {
                data["routeHint"] = recipient.RouteHint;
            }

            // A candidate recipient may get candidate-phrased copy for the same event.
            NotificationTemplates.Message effective = candidateMessage.HasValue && candidateUserId == recipient.UserId
                ? candidateMessage.Value
                : message;

            prepared.Add((new Notification
            {
                Id = Guid.NewGuid(),
                UserId = recipient.UserId,
                EventCode = eventCode,
                Title = effective.Title,
                Body = effective.Body,
                Content = effective.Body,
                DataJson = JsonSerializer.Serialize(data),
                EntityType = recipient.TargetType,
                EntityId = recipient.TargetId,
                Type = typeBucket,
                IsRead = false,
                // Unspecified-kind to match the `timestamp without time zone` column (DbDateTime.Now);
                // a UTC-kind value is rejected by Npgsql for this column type.
                CreatedAt = DbDateTime.Now,
            }, data));
        }

        if (prepared.Count == 0)
        {
            return;
        }

        await _notificationRepository.AddRangeAsync(prepared.Select(item => item.Notification).ToList());
        await _unitOfWork.SaveChangesAsync();

        foreach ((Notification notification, Dictionary<string, object?> data) in prepared)
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
                    // A freshly created notification is, by definition, neither read nor seen. Sending
                    // both flags keeps the SSE payload identical to the GET /api/notifications shape so
                    // the frontend treats a pushed item exactly like a refetched one.
                    IsRead = false,
                    IsSeen = false,
                    CreatedAt = notification.CreatedAt ?? DateTime.UtcNow,
                });
        }
    }

    private async Task<string> ResolveUserNameAsync(Guid userId, string fallback)
    {
        if (userId == Guid.Empty)
        {
            return fallback;
        }

        User? user = await _userRepository.GetByIdAsync(userId);
        return string.IsNullOrWhiteSpace(user?.FullName) ? fallback : user.FullName;
    }

    private static Dictionary<string, object?> BuildApplicationData(
        RecruitPro.Domain.Entities.Application application,
        ApplicationOwnership ownership) => new()
    {
        ["applicationId"] = application.Id,
        ["jobId"] = application.JobId,
        ["jobTitle"] = JobTitle(application),
        ["candidateUserId"] = application.UserId,
        ["candidateName"] = CandidateName(application),
        ["departmentId"] = application.Job?.DepartmentId,
        ["recruiterId"] = ownership.RecruiterUserId,
        ["departmentHeadId"] = ownership.DepartmentHeadUserId,
    };

    private static Dictionary<string, object?> BuildJobData(Job job, Guid? actorUserId) => new()
    {
        ["jobId"] = job.Id,
        ["jobTitle"] = job.Title,
        ["departmentId"] = job.DepartmentId,
        ["recruiterId"] = job.RecruiterId ?? job.CreatedBy,
        ["departmentHeadId"] = job.Department?.HeadUserId,
        ["actorUserId"] = actorUserId,
    };

    private static string CandidateName(RecruitPro.Domain.Entities.Application application)
        => string.IsNullOrWhiteSpace(application.User?.FullName) ? CandidateFallbackName : application.User!.FullName;

    private static string JobTitle(RecruitPro.Domain.Entities.Application application)
        => string.IsNullOrWhiteSpace(application.Job?.Title) ? JobFallbackTitle : application.Job!.Title;

    private static string FormatScheduledAt(DateTime scheduledAt)
        => scheduledAt.ToString("HH:mm 'ngày' dd/MM/yyyy");

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
