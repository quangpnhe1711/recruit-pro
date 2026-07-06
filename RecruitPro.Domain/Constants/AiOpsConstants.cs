namespace RecruitPro.Domain.Constants;

/// <summary>
/// v5 AI Ops feature keys. Every AI call in the system is tagged with one of these so telemetry,
/// prompt registry, provider routing and evaluation all share one vocabulary. Values are stable
/// machine strings — do not rename without a data migration on ai_run_telemetry.feature.
/// </summary>
public static class AiFeatureKeys
{
    public const string Ranking = "ranking";
    public const string CandidateFitAnalysis = "candidate_fit_analysis";
    public const string ResumeParsing = "resume_parsing";
    public const string Embedding = "embedding";
    public const string SemanticScoring = "semantic_scoring";
    public const string CopilotChat = "copilot_chat";
    public const string InterviewQuestions = "interview_questions";
    public const string EmailDraft = "email_draft";
    public const string WorkflowAiAction = "workflow_ai_action";

    /// <summary>All known feature keys, for validation and seeding.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Ranking, CandidateFitAnalysis, ResumeParsing, Embedding, SemanticScoring,
        CopilotChat, InterviewQuestions, EmailDraft, WorkflowAiAction,
    ];

    public static bool IsKnown(string? feature) => feature is not null && All.Contains(feature);
}

/// <summary>
/// v5 risk-flag codes attached to an AI run (stored in ai_run_telemetry.risk_flags_json). A flag is a
/// SIGNAL that a human should review the output — it never asserts the output is wrong. Heuristic only.
/// </summary>
public static class AiRiskFlags
{
    public const string UnsupportedClaim = "unsupported_claim";
    public const string MissingEvidence = "missing_evidence";
    public const string SchemaInvalid = "schema_invalid";
    public const string HighConfidenceLowEvidence = "high_confidence_low_evidence";
    public const string FallbackUsed = "fallback_used";
    public const string ProhibitedCriteriaDetected = "prohibited_criteria_detected";
    public const string ProviderError = "provider_error";

    /// <summary>Severity buckets used by the risk-flags dashboard. Not persisted; derived at read time.</summary>
    public static string Severity(string flag) => flag switch
    {
        SchemaInvalid or ProhibitedCriteriaDetected or ProviderError => "high",
        UnsupportedClaim or HighConfidenceLowEvidence => "medium",
        _ => "low",
    };
}

/// <summary>
/// Fine-grained AI Ops permission keys (v5). The runtime security boundary is currently role-based
/// (<c>[Authorize(Roles=...)]</c>): SystemAdmin owns all AI Ops surfaces; Manager/HeadDepartment own
/// Talent Intelligence. These string keys document the intended fine-grained model and give a future
/// permission-based scheme (claims transformation + policy) a stable vocabulary to bind to. They are
/// NOT enforced on their own today.
/// </summary>
public static class AiOpsPermissions
{
    public const string MetricsView = "ai.metrics.view";
    public const string PromptsView = "ai.prompts.view";
    public const string PromptsManage = "ai.prompts.manage";
    public const string ProviderRoutingView = "ai.provider_routing.view";
    public const string ProviderRoutingManage = "ai.provider_routing.manage";
    public const string EvaluationsView = "ai.evaluations.view";
    public const string EvaluationsRun = "ai.evaluations.run";
    public const string RiskFlagsView = "ai.risk_flags.view";
    public const string TalentIntelligenceView = "talent_intelligence.view";
}
