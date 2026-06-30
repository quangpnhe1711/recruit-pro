namespace RecruitPro.Application.DTOs.Response.Copilot;

public class CopilotJobOptionDto
{
    public Guid JobId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ApplicationCount { get; set; }
}

public class CopilotConversationDto
{
    public Guid ConversationId { get; set; }
    public Guid JobId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? LatestRankingSessionId { get; set; }
}

public class CopilotMessageDto
{
    public Guid MessageId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public int SequenceNo { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CopilotConversationDetailDto : CopilotConversationDto
{
    public IReadOnlyList<CopilotMessageDto> Messages { get; set; } = [];
}

public class CopilotJobContextDto
{
    public Guid JobId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> Requirements { get; set; } = [];
    public IReadOnlyList<string> RequiredSkills { get; set; } = [];
}

public class CopilotCandidateDto
{
    public Guid CandidateUserId { get; set; }
    public Guid ApplicationId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Education { get; set; }
    public int ExperienceYears { get; set; }
    public IReadOnlyList<string> Skills { get; set; } = [];
    public string CvSummary { get; set; } = string.Empty;
    public string? ResumeUrl { get; set; }
}

public class CopilotCandidatePoolDto
{
    public CopilotJobContextDto Job { get; set; } = new();
    public IReadOnlyList<CopilotCandidateDto> Candidates { get; set; } = [];
}

public class CopilotNormalizedRulesDto
{
    public IReadOnlyList<string> RequiredSkills { get; set; } = [];
    public IReadOnlyList<string> PreferredSkills { get; set; } = [];
    public int? MinExperienceYears { get; set; }
    public IReadOnlyList<CopilotAutoRejectRuleDto> AutoRejectRules { get; set; } = [];
    public decimal? MinTotalScore { get; set; }
    public IReadOnlyList<CopilotRuleCriterionDto> PriorityCriteria { get; set; } = [];
    public IReadOnlyList<CopilotRuleCriterionDto> NegativeCriteria { get; set; } = [];
}

public class CopilotRuleCriterionDto
{
    public string Label { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "contains";
    public string Value { get; set; } = string.Empty;
    public string Weight { get; set; } = "medium";
    public bool AutoReject { get; set; }
}

public class CopilotAutoRejectRuleDto
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "contains";
    public string Value { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class CopilotRankingResultDto
{
    public Guid CandidateUserId { get; set; }
    public Guid ApplicationId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int RankPosition { get; set; }
    public decimal TotalScore { get; set; }
    public decimal SkillScore { get; set; }
    public decimal ExperienceScore { get; set; }
    public decimal EducationScore { get; set; }
    public decimal ProjectScore { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public bool IsAutoRejected { get; set; }
    public string? RejectReason { get; set; }
    public IReadOnlyList<string> Strengths { get; set; } = [];
    public IReadOnlyList<string> Weaknesses { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public bool IsAiGenerated { get; set; }
}

public class CopilotPromptResponseDto
{
    public Guid ConversationId { get; set; }
    public Guid? RankingSessionId { get; set; }
    public bool DidRank { get; set; }
    public string AssistantMessage { get; set; } = string.Empty;
    public CopilotNormalizedRulesDto NormalizedRules { get; set; } = new();
    public IReadOnlyList<CopilotRankingResultDto> Results { get; set; } = [];
}

public class CopilotRankingSessionDetailDto
{
    public Guid RankingSessionId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid JobId { get; set; }
    public string UserPrompt { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public int TotalCandidates { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public DateTime? CreatedAt { get; set; }
    public CopilotNormalizedRulesDto NormalizedRules { get; set; } = new();
    public IReadOnlyList<CopilotRankingResultDto> Results { get; set; } = [];
}

public class CopilotSavedRuleDto
{
    public Guid RuleId { get; set; }
    public Guid JobId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public CopilotNormalizedRulesDto Rule { get; set; } = new();
}

public class CopilotAiMetadataDto
{
    public Guid AuditId { get; set; }
    public Guid? ArtifactId { get; set; }
    public bool FallbackUsed { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; set; } = [];
}

public class CopilotPromptTemplateDto
{
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CandidateFitAnalysisSnapshotDto
{
    public Guid FitAnalysisId { get; set; }
    public Guid AuditId { get; set; }
    public Guid JobId { get; set; }
    public Guid CandidateUserId { get; set; }
    public Guid ApplicationId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string FitLabel { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
    public decimal TotalScore { get; set; }
    public IReadOnlyList<string> Strengths { get; set; } = [];
    public IReadOnlyList<string> Gaps { get; set; } = [];
    public IReadOnlyList<string> Evidence { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public bool FallbackUsed { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class CopilotGeneratedArtifactDto
{
    public Guid ArtifactId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid? JobId { get; set; }
    public Guid? ApplicationId { get; set; }
    public string ArtifactType { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public bool FallbackUsed { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class NaturalLanguageCandidateSearchResponseDto
{
    public string NormalizedIntent { get; set; } = "candidate_search";
    public string Query { get; set; } = string.Empty;
    public CopilotNormalizedRulesDto ExtractedFilters { get; set; } = new();
    public IReadOnlyList<CopilotCandidateSearchResultDto> Results { get; set; } = [];
    public CopilotAiMetadataDto Ai { get; set; } = new();
}

public class CopilotCandidateSearchResultDto
{
    public Guid CandidateUserId { get; set; }
    public Guid ApplicationId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public decimal MatchScore { get; set; }
    public IReadOnlyList<string> MatchedSkills { get; set; } = [];
    public IReadOnlyList<string> MissingSkills { get; set; } = [];
    public string Evidence { get; set; } = string.Empty;
}

public class CandidateFitAnalysisResponseDto
{
    public Guid JobId { get; set; }
    public IReadOnlyList<CandidateFitAnalysisDto> Analyses { get; set; } = [];
    public CopilotAiMetadataDto Ai { get; set; } = new();
}

public class CandidateFitAnalysisDto
{
    public Guid CandidateUserId { get; set; }
    public Guid ApplicationId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string FitLabel { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
    public decimal TotalScore { get; set; }
    public IReadOnlyList<string> Strengths { get; set; } = [];
    public IReadOnlyList<string> Gaps { get; set; } = [];
    public IReadOnlyList<string> Evidence { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
}

public class InterviewQuestionSetDto
{
    public Guid JobId { get; set; }
    public Guid? CandidateUserId { get; set; }
    public string Focus { get; set; } = string.Empty;
    public IReadOnlyList<InterviewQuestionDto> Questions { get; set; } = [];
    public CopilotAiMetadataDto Ai { get; set; } = new();
}

public class InterviewQuestionDto
{
    public string Category { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
}

public class ShortlistSuggestionResponseDto
{
    public Guid JobId { get; set; }
    public IReadOnlyList<ShortlistSuggestionDto> Suggestions { get; set; } = [];
    public CopilotAiMetadataDto Ai { get; set; } = new();
}

public class ShortlistSuggestionDto
{
    public Guid CandidateUserId { get; set; }
    public Guid ApplicationId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int RankPosition { get; set; }
    public decimal Score { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public IReadOnlyList<string> Rationale { get; set; } = [];
}

public class HrEmailDraftResponseDto
{
    public Guid ApplicationId { get; set; }
    public string TemplateType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public IReadOnlyList<string> Evidence { get; set; } = [];
    public CopilotAiMetadataDto Ai { get; set; } = new();
}
