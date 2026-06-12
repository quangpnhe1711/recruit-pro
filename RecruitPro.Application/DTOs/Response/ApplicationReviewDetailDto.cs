namespace RecruitPro.Application.DTOs.Response;

public class ApplicationReviewCandidateDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? CurrentPosition { get; set; }
    public int? ExperienceYears { get; set; }
    public string? Education { get; set; }
    public string? Address { get; set; }
    public string? Bio { get; set; }
    public string? LinkedinUrl { get; set; }
    public string? GithubUrl { get; set; }
    public List<string> Skills { get; set; } = [];
}

public class ApplicationReviewJobDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public List<string> RequiredSkills { get; set; } = [];
}

public class ApplicationReviewInterviewDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime InterviewDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ApplicationReviewInsightDto
{
    public int SkillsMatchPercent { get; set; }
    public int MatchedSkillCount { get; set; }
    public int RequiredSkillCount { get; set; }
    public int SubmittedInterviewNotes { get; set; }
    public int TotalInterviews { get; set; }
}

public class ApplicationReviewDetailDto
{
    public string ApplicationId { get; set; } = string.Empty;
    public string ReferenceCode { get; set; } = string.Empty;
    public string StageLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? OfferStatus { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string NextStep { get; set; } = string.Empty;
    public ApplicationReviewCandidateDto Candidate { get; set; } = new();
    public ApplicationReviewJobDto Job { get; set; } = new();
    public ApplicationReviewInsightDto Insights { get; set; } = new();
    public List<ApplicationReviewInterviewDto> Interviews { get; set; } = [];
    public UserDto? ReviewedBy { get; set; }
}
