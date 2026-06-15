namespace RecruitPro.Application.DTOs.Response;

public class HrCandidateDetailDto
{
    public CandidateProfileViewDto Profile { get; set; } = new();
    public List<CandidateSkillViewDto> Skills { get; set; } = [];
    public List<CandidateExperienceDto> ExperienceEntries { get; set; } = [];
    public List<CandidateProjectDto> Projects { get; set; } = [];
    public List<CandidateEducationDto> Educations { get; set; } = [];
    public List<CandidateCertificationDto> Certifications { get; set; } = [];
    public List<CandidateLanguageDto> Languages { get; set; } = [];
    public CandidateResumeDto? Resume { get; set; }
    public List<CandidateResumeDto> ResumeHistory { get; set; } = [];
    public List<HrCandidateApplicationHistoryItemDto> ApplicationHistory { get; set; } = [];
    public List<HrCandidateInterviewHistoryItemDto> InterviewHistory { get; set; } = [];
}

public class HrCandidateApplicationHistoryItemDto
{
    public string ApplicationId { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? AppliedAt { get; set; }
    public int InterviewCount { get; set; }
}

public class HrCandidateInterviewHistoryItemDto
{
    public string InterviewId { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime InterviewDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
