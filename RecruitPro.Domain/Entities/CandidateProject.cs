using System;

namespace RecruitPro.Domain.Entities;

public partial class CandidateProject
{
    public Guid Id { get; set; }

    public Guid CandidateProfileId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Role { get; set; }

    public string? Description { get; set; }

    public string? TechnologiesJson { get; set; }

    public int StartMonth { get; set; }

    public int StartYear { get; set; }

    public int? EndMonth { get; set; }

    public int? EndYear { get; set; }

    public bool IsCurrent { get; set; }

    public virtual CandidateProfile CandidateProfile { get; set; } = null!;
}
