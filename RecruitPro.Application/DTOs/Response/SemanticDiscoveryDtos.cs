namespace RecruitPro.Application.DTOs.Response;

public class SemanticCandidateCardDto
{
    public string CandidateId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Headline { get; set; }
    public string? Location { get; set; }
    public decimal? ExperienceYears { get; set; }
    public decimal SimilarityScore { get; set; }
    public decimal? RuleScore { get; set; }
    public decimal? FinalScore { get; set; }
    public List<string> TopSkills { get; set; } = [];
    public string SummarySnippet { get; set; } = string.Empty;
    public string MatchReason { get; set; } = string.Empty;
}

public class SemanticJobCardDto
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string WorkMode { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public decimal SimilarityScore { get; set; }
    public List<string> TopSkills { get; set; } = [];
    public string SummarySnippet { get; set; } = string.Empty;
}

public class SemanticCandidatesResponseDto
{
    public List<SemanticCandidateCardDto> Items { get; set; } = [];
    public ApiEnvelopeMeta Meta { get; set; } = new();
}

public class SemanticJobsResponseDto
{
    public List<SemanticJobCardDto> Items { get; set; } = [];
    public ApiEnvelopeMeta Meta { get; set; } = new();
}
