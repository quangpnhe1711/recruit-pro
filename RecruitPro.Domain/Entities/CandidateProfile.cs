using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class CandidateProfile
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string? CurrentPosition { get; set; }

    public int? ExperienceYears { get; set; }

    public string? Education { get; set; }

    public string? Address { get; set; }

    public string? Bio { get; set; }

    public string? ResumeUrl { get; set; }

    public string? ExperienceEntriesJson { get; set; }

    public string? EducationRecordsJson { get; set; }

    public string? CertificationRecordsJson { get; set; }

    public string? LanguageRecordsJson { get; set; }

    public string? GithubUrl { get; set; }

    public string? LinkedinUrl { get; set; }

    public string? ParsedResumeJson { get; set; }

    public string? ResumeExtractedText { get; set; }

    public string? ResumeParseStatus { get; set; }

    public string? ResumeParseError { get; set; }

    public string? ResumeParseModel { get; set; }

    public string? ResumeParserWarningsJson { get; set; }

    public DateTime? ResumeParsedAt { get; set; }

    public string? CandidateEmbeddingVectorJson { get; set; }

    public string? CandidateEmbeddingTextHash { get; set; }

    public string? CandidateEmbeddingStatus { get; set; }

    public string? CandidateEmbeddingError { get; set; }

    public DateTime? CandidateEmbeddingUpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<CandidateSkill> CandidateSkills { get; set; } = new List<CandidateSkill>();

    public virtual ICollection<CandidateProject> Projects { get; set; } = new List<CandidateProject>();

    public virtual ICollection<CandidateResume> Resumes { get; set; } = new List<CandidateResume>();

    public virtual ICollection<CandidateProfileSection> Sections { get; set; } = new List<CandidateProfileSection>();

}
