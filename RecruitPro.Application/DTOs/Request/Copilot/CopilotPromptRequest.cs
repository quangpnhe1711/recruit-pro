namespace RecruitPro.Application.DTOs.Request.Copilot;

public class CopilotPromptRequest
{
    public Guid JobId { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public bool ForceRanking { get; set; }
    public bool UseLatestRankingContext { get; set; } = true;
    public IReadOnlyList<CopilotRuleCriterionRequestDto> PriorityCriteria { get; set; } = [];
    public IReadOnlyList<CopilotRuleCriterionRequestDto> NegativeCriteria { get; set; } = [];
}

public class NaturalLanguageCandidateSearchRequest
{
    public Guid JobId { get; set; }
    public string Query { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 10;
}

public class CandidateFitAnalysisRequest
{
    public IReadOnlyList<Guid> CandidateUserIds { get; set; } = [];
    public IReadOnlyList<Guid> ApplicationIds { get; set; } = [];
    public string Prompt { get; set; } = string.Empty;
}

public class InterviewQuestionRequest
{
    public Guid? CandidateUserId { get; set; }
    public Guid? ApplicationId { get; set; }
    public string Focus { get; set; } = string.Empty;
    public int QuestionCount { get; set; } = 8;
}

public class ShortlistRequest
{
    public string Prompt { get; set; } = string.Empty;
    public int MaxCandidates { get; set; } = 5;
}

public class HrEmailDraftRequest
{
    public string TemplateType { get; set; } = "screening_follow_up";
    public string Tone { get; set; } = "professional";
    public string AdditionalInstruction { get; set; } = string.Empty;
}

public class CreateCopilotPromptTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string TemplateType { get; set; } = "general";
    public string Prompt { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
