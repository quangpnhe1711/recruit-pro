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

    // Resume / CV upload domain (upload hardening)
    public const string ResumeFileRequired = "RESUME_FILE_REQUIRED";
    public const string ResumeFileEmpty = "RESUME_FILE_EMPTY";
    public const string ResumeFileTooLarge = "RESUME_FILE_TOO_LARGE";
    public const string ResumeFileUnsupportedType = "RESUME_FILE_UNSUPPORTED_TYPE";

    // Job posting rules
    public const string JobDeadlineInvalid = "JOB_DEADLINE_INVALID";

    // System Admin / RBAC domain
    public const string RbacUnknownPermission = "RBAC_UNKNOWN_PERMISSION";
    public const string RbacAdminLockout = "RBAC_ADMIN_LOCKOUT";
    public const string RbacRoleNotFound = "RBAC_ROLE_NOT_FOUND";
    public const string UserStatusInvalid = "USER_STATUS_INVALID";
    public const string UserSelfDeactivation = "USER_SELF_DEACTIVATION";
    public const string AccountDisabled = "ACCOUNT_DISABLED";

    // Cross-cutting
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string ValidationError = "VALIDATION_ERROR";

    // ---------------------------------------------------------------------------------------------
    // Code-first error contract (error-code + localized-message refactor). Every error response now
    // carries a stable `code`; the human `message` is resolved centrally from the code via
    // IErrorMessageProvider (never hardcoded at the throw/return site). The frontend ignores the
    // backend message for UI and maps `code` (+ params) to its own i18n dictionary.
    // ---------------------------------------------------------------------------------------------

    // Generic validation / bad input
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string FormInvalid = "FORM_INVALID";
    public const string InvalidInput = "INVALID_INPUT";
    public const string Required = "REQUIRED";
    public const string InvalidEmail = "INVALID_EMAIL";
    public const string InvalidFormat = "INVALID_FORMAT";
    public const string MaxLengthExceeded = "MAX_LENGTH_EXCEEDED";
    public const string MinLengthRequired = "MIN_LENGTH_REQUIRED";
    public const string OutOfRange = "OUT_OF_RANGE";
    public const string InvalidAmount = "INVALID_AMOUNT";

    // Account / credentials
    public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";
    public const string UsernameAlreadyExists = "USERNAME_ALREADY_EXISTS";
    public const string PasswordTooWeak = "PASSWORD_TOO_WEAK";
    public const string PasswordTooShort = "PASSWORD_TOO_SHORT";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string PortalAccessDenied = "PORTAL_ACCESS_DENIED";

    // Not-found family (specific codes so the FE can copy per entity; all fall back to ENTITY_NOT_FOUND)
    public const string EntityNotFound = "ENTITY_NOT_FOUND";
    public const string JobNotFound = "JOB_NOT_FOUND";
    public const string CandidateNotFound = "CANDIDATE_NOT_FOUND";
    public const string CandidateProfileNotFound = "CANDIDATE_PROFILE_NOT_FOUND";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string InterviewNotFound = "INTERVIEW_NOT_FOUND";
    public const string ResumeNotFound = "RESUME_NOT_FOUND";
    public const string RoleNotFound = "ROLE_NOT_FOUND";
    public const string CandidateRoleNotFound = "CANDIDATE_ROLE_NOT_FOUND";
    public const string ExperienceNotFound = "EXPERIENCE_NOT_FOUND";
    public const string NotificationNotFound = "NOTIFICATION_NOT_FOUND";
    public const string WorkflowNotFound = "WORKFLOW_NOT_FOUND";
    public const string WorkflowExecutionNotFound = "WORKFLOW_EXECUTION_NOT_FOUND";
    public const string McpToolNotFound = "MCP_TOOL_NOT_FOUND";
    public const string OfferNotFound = "OFFER_NOT_FOUND";

    // Conflict / duplicate
    public const string Conflict = "CONFLICT";
    public const string DuplicateEntity = "DUPLICATE_ENTITY";

    // Business rules (generic + specific)
    public const string BusinessRuleViolation = "BUSINESS_RULE_VIOLATION";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string JobClosed = "JOB_CLOSED";
    public const string SalaryRangeInvalid = "SALARY_RANGE_INVALID";
    public const string DateMustBeInFuture = "DATE_MUST_BE_IN_FUTURE";
    public const string InterviewTimeInvalid = "INTERVIEW_TIME_INVALID";
    public const string InterviewTimeInPast = "INTERVIEW_TIME_IN_PAST";
    public const string OfferAlreadySent = "OFFER_ALREADY_SENT";

    // File / upload (generic; resume-specific codes above stay for the CV pipeline)
    public const string FileRequired = "FILE_REQUIRED";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string UnsupportedFileType = "UNSUPPORTED_FILE_TYPE";
    public const string FileUploadFailed = "FILE_UPLOAD_FAILED";

    // AI / provider
    public const string AiProviderUnavailable = "AI_PROVIDER_UNAVAILABLE";
    public const string AiProcessingFailed = "AI_PROCESSING_FAILED";

    // System / transport (FE also owns network/timeout, which never originate from the server)
    public const string ServerError = "SERVER_ERROR";
    public const string UnexpectedError = "UNEXPECTED_ERROR";
    public const string NetworkError = "NETWORK_ERROR";
    public const string TimeoutError = "TIMEOUT_ERROR";
}
