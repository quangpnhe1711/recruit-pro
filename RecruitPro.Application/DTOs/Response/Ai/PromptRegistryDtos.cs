namespace RecruitPro.Application.DTOs.Response.Ai;

/// <summary>v5.3 — one AI feature's prompt state for the registry list.</summary>
public class PromptFeatureSummaryDto
{
    public string FeatureKey { get; set; } = string.Empty;
    public int? ActiveVersionNo { get; set; }
    public string? ActiveVersionName { get; set; }
    public Guid? ActiveVersionId { get; set; }
    public int TotalVersions { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}

/// <summary>v5.3 — a single immutable prompt version.</summary>
public class PromptVersionDto
{
    public Guid Id { get; set; }
    public string FeatureKey { get; set; } = string.Empty;
    public int VersionNo { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TemplateBody { get; set; } = string.Empty;
    public List<string> Variables { get; set; } = [];
    public bool IsActive { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ActivatedBy { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public string? Notes { get; set; }
}
