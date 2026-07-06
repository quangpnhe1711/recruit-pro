namespace RecruitPro.Domain.Entities;

/// <summary>
/// v5.3 — a versioned, immutable system prompt for an AI feature. Distinct from
/// <see cref="CopilotPromptTemplate"/> (HR-owned artifact prompts): this is operational prompt
/// governance for the platform's AI features. Editing a prompt means creating a NEW version;
/// existing versions are never mutated. Exactly one version per feature_key is active at a time;
/// rollback = re-activating an older version.
/// </summary>
public class PromptTemplateVersion
{
    public Guid Id { get; set; }

    /// <summary>Feature this prompt drives. One of <see cref="Constants.AiFeatureKeys"/>.</summary>
    public string FeatureKey { get; set; } = string.Empty;

    /// <summary>Monotonic per feature_key; (feature_key, version_no) is unique.</summary>
    public int VersionNo { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string TemplateBody { get; set; } = string.Empty;

    /// <summary>jsonb array of declared variable names, e.g. ["job","candidate"].</summary>
    public string VariablesJson { get; set; } = "[]";

    public bool IsActive { get; set; }

    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid? ActivatedBy { get; set; }
    public DateTime? ActivatedAt { get; set; }

    public string? Notes { get; set; }
}
