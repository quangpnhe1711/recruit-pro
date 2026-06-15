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

    public virtual User User { get; set; } = null!;

    public virtual ICollection<Skill> Skills { get; set; } = new List<Skill>();

    public virtual ICollection<CandidateProject> Projects { get; set; } = new List<CandidateProject>();

    public virtual ICollection<CandidateResume> Resumes { get; set; } = new List<CandidateResume>();

    public virtual ICollection<CandidateSkillDetail> CandidateSkillDetails { get; set; } = new List<CandidateSkillDetail>();
}
