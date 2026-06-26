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

    // Cross-cutting
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string ValidationError = "VALIDATION_ERROR";
}
