namespace RecruitPro.Application.Notifications;

/// <summary>
/// Builds the click-ready frontend deep-link URLs carried in notification payloads. URLs are FRONTEND
/// routes (verified against recruit-pro-internal's React router), never API routes. Links are
/// role-aware: a candidate is never routed to an HR/Manager screen and vice-versa
/// (NOTIFICATION-EVENT-MATRIX.md §Role-aware URL rule).
///
/// Route inventory (recruit-pro-internal/src/routes):
///   Candidate : /candidate/my-applications, /candidate/interviews
///   HR        : /hr/applications/{id}, /hr/applications/{id}/send-offer,
///               /hr/interviews, /hr/interviews/schedule?applicationId={id}
///   Manager   : /manager/applications/{id}, /manager/jobs/{id}/approval
///   Public    : /jobs/{id}
///
/// Documented fallbacks (no dedicated route exists yet):
///   - Candidate application/offer detail  -> /candidate/my-applications (list; no detail-by-id route).
///   - Candidate interview detail          -> /candidate/interviews (list; no detail-by-id route).
///   - HR/Recruiter job detail             -> /jobs/{id} (public job detail; no internal HR job-detail route).
/// </summary>
public static class NotificationLinks
{
    // ----- Candidate-safe links -----

    /// <summary>Candidate's own application detail. Falls back to the My Applications list (no detail route).</summary>
    public static string CandidateApplication(Guid applicationId)
        => $"/candidate/my-applications?applicationId={applicationId}";

    /// <summary>Candidate's interview view. Falls back to the interviews list (no detail route).</summary>
    public static string CandidateInterview(Guid interviewId)
        => $"/candidate/interviews?interviewId={interviewId}";

    /// <summary>Candidate's offer view — surfaced inside the application detail; no candidate-only offer route.</summary>
    public static string CandidateOffer(Guid applicationId)
        => $"/candidate/my-applications?applicationId={applicationId}";

    // ----- HR / Recruiter-safe links -----

    /// <summary>HR application / candidate review detail.</summary>
    public static string HrApplication(Guid applicationId)
        => $"/hr/applications/{applicationId}";

    /// <summary>HR interview scheduling screen for a specific application.</summary>
    public static string HrInterviewSchedule(Guid applicationId)
        => $"/hr/interviews/schedule?applicationId={applicationId}";

    /// <summary>HR interview detail — falls back to the interviews list (no detail route).</summary>
    public static string HrInterview(Guid interviewId)
        => $"/hr/interviews?interviewId={interviewId}";

    /// <summary>HR/Recruiter job detail — falls back to the public job page (no internal HR job-detail route).</summary>
    public static string HrJob(Guid jobId)
        => $"/jobs/{jobId}";

    // ----- DepartmentHead / Manager-safe links -----

    /// <summary>DepartmentHead/Manager candidate review detail.</summary>
    public static string HeadApplicationReview(Guid applicationId)
        => $"/manager/applications/{applicationId}";

    /// <summary>DepartmentHead/Manager job approval detail.</summary>
    public static string HeadJobApproval(Guid jobId)
        => $"/manager/jobs/{jobId}/approval";
}
