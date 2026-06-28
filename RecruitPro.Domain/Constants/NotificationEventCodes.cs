namespace RecruitPro.Domain.Constants;

/// <summary>
/// Canonical notification event codes for the recruitment workflow (NOTIFICATION-EVENT-MATRIX.md).
/// These are stored verbatim in <c>notifications.event_code</c> and are the click-routing key the
/// frontend resolver keys off. Codes are append-only: legacy codes are kept for backward compatibility
/// with existing rows and must not be renamed/deleted.
/// </summary>
public static class NotificationEventCodes
{
    // Job workflow
    public const string JobSubmittedForApproval = "job_submitted_for_approval";
    public const string JobApproved = "job_approved";
    public const string JobRejected = "job_rejected";

    // Application workflow
    public const string ApplicationApplied = "application_applied";
    public const string ApplicationScreeningStarted = "application_screening_started";
    public const string ApplicationDepartmentHeadReviewRequested = "application_department_head_review_requested";
    public const string ApplicationInterviewRequested = "application_interview_requested";
    public const string ApplicationWithdrawn = "application_withdrawn";

    // Interview workflow
    public const string InterviewScheduled = "interview_scheduled";
    public const string InterviewCompleted = "interview_completed";

    // Offer / rejection (email-gated) workflow
    public const string OfferEmailSent = "offer_email_sent";
    public const string RejectionEmailSent = "rejection_email_sent";
    public const string OfferAccepted = "offer_accepted";
    public const string OfferDeclined = "offer_declined";

    /// <summary>
    /// Defined for completeness, but intentionally NOT emitted: in this model accepting an offer hires
    /// the candidate in the same action, so <see cref="OfferAccepted"/> is emitted instead to avoid
    /// duplicate noise (Option A in NOTIFICATION-EVENT-MATRIX.md §candidate_hired).
    /// </summary>
    public const string CandidateHired = "candidate_hired";

    // Legacy codes — preserved for backward compatibility with existing rows / score pipeline.
    public const string CandidateScoreReady = "candidate_score_ready";
}
