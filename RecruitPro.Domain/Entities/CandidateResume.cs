using System;

namespace RecruitPro.Domain.Entities;

public partial class CandidateResume
{
    public Guid Id { get; set; }

    public Guid CandidateProfileId { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public int Version { get; set; }

    public DateTime UploadDate { get; set; }

    public bool IsCurrent { get; set; }

    public virtual CandidateProfile CandidateProfile { get; set; } = null!;
}
