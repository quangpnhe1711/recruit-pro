namespace RecruitPro.Domain.Automation;

/// <summary>
/// Durable domain event types published by ATS services into the outbox. The trigger of a workflow
/// definition names one of these. Kept as strings (not an enum) so a new event can be added without a
/// schema change and workflow trigger JSON stays human-readable.
/// </summary>
public static class WorkflowEventTypes
{
    public const string CandidateApplied = "CandidateApplied";
    public const string PassedToHeadReview = "PassedToHeadReview";
    public const string InterviewCompleted = "InterviewCompleted";
    public const string JobApproved = "JobApproved";
    public const string CandidateScoreReady = "CandidateScoreReady";
    public const string HeadReviewOverdue = "HeadReviewOverdue";

    public static readonly string[] All =
    [
        CandidateApplied,
        PassedToHeadReview,
        InterviewCompleted,
        JobApproved,
        CandidateScoreReady,
        HeadReviewOverdue
    ];
}

/// <summary>Registered, deterministic, safe action handler keys.</summary>
public static class WorkflowActionType
{
    public const string NotifyUser = "notify_user";
    public const string NotifyRole = "notify_role";
    public const string SendReminder = "send_reminder";
    public const string RuleBasedNextStepSuggestion = "rule_based_next_step_suggestion";
    public const string ShadowLog = "shadow_log";

    public static readonly string[] All =
    [
        NotifyUser,
        NotifyRole,
        SendReminder,
        RuleBasedNextStepSuggestion,
        ShadowLog
    ];
}

/// <summary>Step-type labels used in execution step logs.</summary>
public static class WorkflowStepType
{
    public const string ConditionEvaluation = "condition";
    public const string Action = "action";
}

/// <summary>
/// Recipient selectors used by notify_user / send_reminder action config. They resolve against the
/// event payload's ownership fields — never a broad broadcast.
/// </summary>
public static class WorkflowRecipientSelector
{
    public const string AssignedDepartmentHead = "assignedDepartmentHead";
    public const string AssignedRecruiter = "assignedRecruiter";
    public const string Candidate = "candidate";
}

/// <summary>Read-only MCP-style internal tool names.</summary>
public static class McpToolNames
{
    public const string JobsSearch = "jobs.search";
    public const string JobsGet = "jobs.get";
    public const string ApplicationsGet = "applications.get";
    public const string ApplicationsGetFitAnalysis = "applications.get_fit_analysis";
    public const string InterviewsGetSchedule = "interviews.get_schedule";
    public const string AnalyticsGetFunnelSummary = "analytics.get_funnel_summary";

    public static readonly string[] All =
    [
        JobsSearch,
        JobsGet,
        ApplicationsGet,
        ApplicationsGetFitAnalysis,
        InterviewsGetSchedule,
        AnalyticsGetFunnelSummary
    ];
}
