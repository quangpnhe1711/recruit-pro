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
    public List<CandidateProfileSectionDto> Sections { get; set; } = [];
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

public class CandidateProfileSectionDto
{
    public string Id { get; set; } = string.Empty;
    public string? SectionKey { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SectionType { get; set; } = "Custom";
    public string Source { get; set; } = "User";
    public int DisplayOrder { get; set; }
    public Dictionary<string, string> Schema { get; set; } = [];
    public List<CandidateProfileSectionItemDto> Items { get; set; } = [];
}

public class CandidateProfileSectionItemDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemType { get; set; } = "Entry";
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Organization { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? DateLabel { get; set; }
    public int? StartMonth { get; set; }
    public int? StartYear { get; set; }
    public int? EndMonth { get; set; }
    public int? EndYear { get; set; }
    public bool IsCurrent { get; set; }
    public int DisplayOrder { get; set; }
    public List<string> Tags { get; set; } = [];
    public Dictionary<string, string> Attributes { get; set; } = [];
}
