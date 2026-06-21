using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class CandidateProfileSection
{
    public Guid Id { get; set; }

    public Guid CandidateProfileId { get; set; }

    public string? SectionKey { get; set; }

    public string Title { get; set; } = string.Empty;

    public string SectionType { get; set; } = "Custom";

    public string Source { get; set; } = "User";

    public int DisplayOrder { get; set; }

    public string? SchemaJson { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual CandidateProfile CandidateProfile { get; set; } = null!;

    public virtual ICollection<CandidateProfileSectionItem> Items { get; set; } = new List<CandidateProfileSectionItem>();
}
