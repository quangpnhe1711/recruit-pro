namespace RecruitPro.Domain.Entities;

/// <summary>
/// v5.4 — an offline evaluation case for an AI feature: a fixed input, an optional expected output and
/// an optional scoring rubric. Cases are sanitized/synthetic test data (never live production payloads)
/// so evaluation can run without touching real candidates.
/// </summary>
public class AiEvaluationCase
{
    public Guid Id { get; set; }

    /// <summary>Feature this case exercises. One of <see cref="Constants.AiFeatureKeys"/>.</summary>
    public string FeatureKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>jsonb input payload fed to the feature under evaluation.</summary>
    public string InputJson { get; set; } = "{}";

    /// <summary>jsonb expected output (optional).</summary>
    public string? ExpectedJson { get; set; }

    /// <summary>jsonb scoring rubric (optional).</summary>
    public string? ScoringRubricJson { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}
