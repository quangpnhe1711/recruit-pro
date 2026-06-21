using System;

namespace RecruitPro.Domain.Entities;

public partial class CandidateProfileSectionItem
{
    public Guid Id { get; set; }

    public Guid SectionId { get; set; }

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

    public string? TagsJson { get; set; }

    public string? AttributesJson { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual CandidateProfileSection Section { get; set; } = null!;
}
