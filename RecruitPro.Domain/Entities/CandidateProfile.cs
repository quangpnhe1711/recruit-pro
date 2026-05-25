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

    public string? GithubUrl { get; set; }

    public string? LinkedinUrl { get; set; }

    public virtual ICollection<Application> Applications { get; set; } = new List<Application>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<Skill> Skills { get; set; } = new List<Skill>();
}
