namespace RecruitPro.Application.Common;

/// <summary>
/// Stable, machine-readable error codes for the API envelope (ERROR-CONTRACT.md). These are part of
/// the contract: the frontend branches on <c>errorCode</c> first, HTTP status second, message last.
/// Codes are stable strings and must not be renamed without updating the frontend and tests.
/// </summary>
public static class ErrorCodes
{
    // Application / apply domain
    public const string ApplicationAlreadyActive = "APPLICATION_ALREADY_ACTIVE";
    public const string ApplicationAlreadyHired = "APPLICATION_ALREADY_HIRED";
    public const string JobNotAcceptingApplications = "JOB_NOT_ACCEPTING_APPLICATIONS";
    public const string JobDeadlinePassed = "JOB_DEADLINE_PASSED";
    public const string CandidateProfileIncomplete = "CANDIDATE_PROFILE_INCOMPLETE";
    public const string ResumeRequired = "RESUME_REQUIRED";
    public const string ApplicationNotFound = "APPLICATION_NOT_FOUND";
    public const string ApplicationNotWithdrawable = "APPLICATION_NOT_WITHDRAWABLE";
    public const string InvalidApplicationTransition = "INVALID_APPLICATION_TRANSITION";
    public const string InterviewNotActionable = "INTERVIEW_NOT_ACTIONABLE";
    public const string OfferNotActionable = "OFFER_NOT_ACTIONABLE";

    // Post-interview decision gates: an application in the Interview stage must have a scheduled and
    // completed interview before it can be offered or rejected.
    public const string InterviewRequired = "INTERVIEW_REQUIRED";
    public const string InterviewNotCompleted = "INTERVIEW_NOT_COMPLETED";

    // Email-gated transitions: Offer and Rejected can only be reached through their email-sending flow,
    // never via a direct status update.
    public const string EmailRequiredForOffer = "EMAIL_REQUIRED_FOR_OFFER";
    public const string EmailRequiredForRejection = "EMAIL_REQUIRED_FOR_REJECTION";
    public const string EmailSendFailed = "EMAIL_SEND_FAILED";

    // Ownership / job-approval domain (Phase 2/3)
    public const string DepartmentHeadRequired = "DEPARTMENT_HEAD_REQUIRED";
    public const string InvalidDepartmentHead = "INVALID_DEPARTMENT_HEAD";
    public const string JobRecruiterRequired = "JOB_RECRUITER_REQUIRED";
    public const string InvalidJobRecruiter = "INVALID_JOB_RECRUITER";
    public const string InvalidJobTransition = "INVALID_JOB_TRANSITION";
    public const string DepartmentNotFound = "DEPARTMENT_NOT_FOUND";

    // Cross-cutting
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string ValidationError = "VALIDATION_ERROR";
}
