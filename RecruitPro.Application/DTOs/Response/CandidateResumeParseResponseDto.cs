namespace RecruitPro.Application.DTOs.Response;

public class CandidateResumeParseResponseDto
{
    public bool UsedAi { get; set; }
    public string ParsingMode { get; set; } = "Heuristic";
    public string? ModelName { get; set; }
    public string? AiFallbackReason { get; set; }
    public CandidateResumeParseProfileDto Profile { get; set; } = new();
    public List<CandidateSkillViewDto> Skills { get; set; } = [];
    public List<CandidateExperienceDto> ExperienceEntries { get; set; } = [];
    public List<CandidateProjectDto> Projects { get; set; } = [];
    public List<CandidateEducationDto> Educations { get; set; } = [];
    public List<CandidateCertificationDto> Certifications { get; set; } = [];
    public List<CandidateLanguageDto> Languages { get; set; } = [];
    public List<string> Notes { get; set; } = [];
    public string ExtractedTextPreview { get; set; } = string.Empty;
}

public class CandidateResumeAiParseDto
{
    public CandidateResumeAiProfileDto Profile { get; set; } = new();
    public List<CandidateResumeAiSkillDto> Skills { get; set; } = [];
    public List<CandidateResumeAiExperienceDto> ExperienceEntries { get; set; } = [];
    public List<CandidateResumeAiProjectDto> Projects { get; set; } = [];
    public List<CandidateResumeAiEducationDto> Educations { get; set; } = [];
    public List<CandidateResumeAiCertificationDto> Certifications { get; set; } = [];
    public List<CandidateResumeAiLanguageDto> Languages { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}

public class CandidateResumeAiProfileDto
{
    public string? Name { get; set; }
    public string? Headline { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public string? Bio { get; set; }
    public string? Github { get; set; }
    public string? Linkedin { get; set; }
}

public class CandidateResumeAiSkillDto
{
    public string Name { get; set; } = string.Empty;
    public decimal? YearsOfExperience { get; set; }
}

public class CandidateResumeAiExperienceDto
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public int? StartMonth { get; set; }
    public int? StartYear { get; set; }
    public int? EndMonth { get; set; }
    public int? EndYear { get; set; }
    public bool IsCurrent { get; set; }
    public List<string> Bullets { get; set; } = [];
}

public class CandidateResumeAiProjectDto
{
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Description { get; set; }
    public List<string> Technologies { get; set; } = [];
    public int? StartMonth { get; set; }
    public int? StartYear { get; set; }
    public int? EndMonth { get; set; }
    public int? EndYear { get; set; }
    public bool IsCurrent { get; set; }
}

public class CandidateResumeAiEducationDto
{
    public string School { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string? FieldOfStudy { get; set; }
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public string? Description { get; set; }
}

public class CandidateResumeAiCertificationDto
{
    public string Name { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public DateTime? IssuedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public string? CredentialId { get; set; }
    public string? CredentialUrl { get; set; }
}

public class CandidateResumeAiLanguageDto
{
    public string Name { get; set; } = string.Empty;
    public string Proficiency { get; set; } = string.Empty;
}

public class CandidateResumeParseProfileDto
{
    public string Name { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Github { get; set; } = string.Empty;
    public string Linkedin { get; set; } = string.Empty;
}
