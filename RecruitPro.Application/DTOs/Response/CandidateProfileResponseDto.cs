namespace RecruitPro.Application.DTOs.Response;

public class CandidateProfileResponseDto
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
    public CandidateResumeParsingStatusDto ResumeParsing { get; set; } = new();
}

public class CandidateResumeParsingStatusDto
{
    public string Status { get; set; } = "NotStarted";
    public string? Error { get; set; }
    public string? Model { get; set; }
    public DateTime? ParsedAt { get; set; }
    public List<string> Warnings { get; set; } = [];
}
