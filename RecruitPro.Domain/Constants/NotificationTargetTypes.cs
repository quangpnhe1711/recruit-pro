namespace RecruitPro.Domain.Constants;

/// <summary>
/// The kind of entity a notification's deep link points at. Carried in the notification payload as
/// <c>targetType</c> (and mirrored onto <c>notifications.entity_type</c>) so the frontend resolver can
/// route/render the click target. See NOTIFICATION-EVENT-MATRIX.md.
/// </summary>
public static class NotificationTargetTypes
{
    public const string Job = "job";
    public const string JobApproval = "job_approval";
    public const string Application = "application";
    public const string ApplicationReview = "application_review";
    public const string Interview = "interview";
    public const string InterviewRequest = "interview_request";
    public const string Offer = "offer";
    public const string Candidate = "candidate";
}

/// <summary>
/// Legacy coarse "type" bucket stored on <c>notifications.type</c> (System/Job/Interview/Application).
/// Kept distinct from <see cref="NotificationTargetTypes"/> which drives deep-link routing.
/// </summary>
public static class NotificationTypeBuckets
{
    public const string Application = "APPLICATION";
    public const string Interview = "INTERVIEW";
    public const string Job = "JOB";
    public const string Offer = "OFFER";
    public const string System = "SYSTEM";
}
