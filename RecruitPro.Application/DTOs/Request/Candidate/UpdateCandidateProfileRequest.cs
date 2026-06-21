namespace RecruitPro.Application.DTOs.Request.Candidate;

public class UpdateCandidateProfileRequest
{
    public string? Name { get; set; }

    public string? Headline { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Location { get; set; }

    public string? Bio { get; set; }

    public string? Github { get; set; }

    public string? Linkedin { get; set; }

    public List<CandidateSkillUpsertRequest>? Skills { get; set; }

    public List<CandidateExperienceUpsertItemRequest>? ExperienceEntries { get; set; }

    public List<CandidateProjectUpsertRequest>? Projects { get; set; }

    public List<CandidateEducationUpsertRequest>? Educations { get; set; }

    public List<CandidateCertificationUpsertRequest>? Certifications { get; set; }

    public List<CandidateLanguageUpsertRequest>? Languages { get; set; }

    public List<CandidateProfileSectionUpsertRequest>? Sections { get; set; }
}

public class CandidateSkillUpsertRequest
{
    public string SkillId { get; set; } = string.Empty;
    public decimal? YearsOfExperience { get; set; }
}

public class CandidateExperienceUpsertItemRequest
{
    public string? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public CandidateExperiencePeriodRequest Period { get; set; } = new();
    public List<string> Bullets { get; set; } = [];
}

public class CandidateProjectUpsertRequest
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Description { get; set; }
    public List<string> Technologies { get; set; } = [];
    public CandidateExperiencePeriodRequest Period { get; set; } = new();
}

public class CandidateEducationUpsertRequest
{
    public string? Id { get; set; }
    public string School { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string? FieldOfStudy { get; set; }
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public string? Description { get; set; }
}

public class CandidateCertificationUpsertRequest
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public DateTime? IssuedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public string? CredentialId { get; set; }
    public string? CredentialUrl { get; set; }
}

public class CandidateLanguageUpsertRequest
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Proficiency { get; set; } = string.Empty;
}

public class CandidateProfileSectionUpsertRequest
{
    public string? Id { get; set; }
    public string? SectionKey { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SectionType { get; set; } = "Custom";
    public string Source { get; set; } = "User";
    public int DisplayOrder { get; set; }
    public Dictionary<string, string>? Schema { get; set; }
    public List<CandidateProfileSectionItemUpsertRequest> Items { get; set; } = [];
}

public class CandidateProfileSectionItemUpsertRequest
{
    public string? Id { get; set; }
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
    public Dictionary<string, string>? Attributes { get; set; }
}
