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
