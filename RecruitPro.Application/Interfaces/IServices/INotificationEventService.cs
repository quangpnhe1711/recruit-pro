using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Interfaces.IServices;

/// <summary>
/// Publishes ownership-routed, click-ready recruitment notifications (NOTIFICATION-EVENT-MATRIX.md).
/// Every publish is a best-effort, post-commit side effect: callers invoke it AFTER the business
/// transaction has committed and wrap the call so a publishing failure never fails the business action.
/// Recipients are resolved from the application/job ownership snapshot (not broad role broadcast) and
/// every notification carries a role-aware frontend deep link (url/targetType/targetId).
/// </summary>
public interface INotificationEventService
{
    // ----- Job workflow -----
    Task PublishJobSubmittedForApprovalAsync(Job job, Guid actorUserId);
    Task PublishJobApprovedAsync(Job job);
    Task PublishJobRejectedAsync(Job job, string? reason = null);

    // ----- Application workflow -----
    Task PublishApplicationAppliedAsync(RecruitPro.Domain.Entities.Application application);
    Task PublishScreeningStartedAsync(RecruitPro.Domain.Entities.Application application);
    Task PublishDepartmentHeadReviewRequestedAsync(RecruitPro.Domain.Entities.Application application, Guid? actorUserId);
    Task PublishInterviewRequestedAsync(RecruitPro.Domain.Entities.Application application);
    Task PublishApplicationWithdrawnAsync(RecruitPro.Domain.Entities.Application application, ApplicationStatus statusBeforeWithdraw);

    // ----- Interview workflow -----
    Task PublishInterviewScheduledAsync(RecruitPro.Domain.Entities.Application application, Interview interview, Guid? interviewerId);
    Task PublishInterviewCompletedAsync(RecruitPro.Domain.Entities.Application application, Interview interview);

    // ----- Offer / rejection (email-gated) workflow -----
    Task PublishOfferEmailSentAsync(RecruitPro.Domain.Entities.Application application, ApplicationOffer offer);
    Task PublishRejectionEmailSentAsync(RecruitPro.Domain.Entities.Application application);
    Task PublishOfferAcceptedAsync(RecruitPro.Domain.Entities.Application application, ApplicationOffer? offer);
    Task PublishOfferDeclinedAsync(RecruitPro.Domain.Entities.Application application, ApplicationOffer? offer);

    // ----- Legacy (preserved for backward compatibility) -----
    Task PublishApplicationStatusChangedAsync(RecruitPro.Domain.Entities.Application application, ApplicationStatus previousStatus);
    Task PublishCandidateScoreReadyAsync(RecruitPro.Domain.Entities.Application application);
}
