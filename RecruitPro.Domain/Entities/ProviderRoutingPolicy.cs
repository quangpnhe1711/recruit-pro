namespace RecruitPro.Domain.Entities;

/// <summary>
/// v5.4 — declares which provider/model an AI feature should use, plus an optional fallback provider
/// and cost/latency guardrails. One policy per feature_key. This is a governance/observability record:
/// the single configured runtime provider still lives in AiProvider settings, so a policy documents and
/// governs intended routing and lets an operator simulate/validate a change before it is wired in.
/// </summary>
public class ProviderRoutingPolicy
{
    public Guid Id { get; set; }

    /// <summary>Feature governed. Unique. One of <see cref="Constants.AiFeatureKeys"/>.</summary>
    public string FeatureKey { get; set; } = string.Empty;

    public string PrimaryProvider { get; set; } = string.Empty;
    public string PrimaryModel { get; set; } = string.Empty;

    public string? FallbackProvider { get; set; }
    public string? FallbackModel { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int? MaxLatencyMs { get; set; }
    public decimal? MaxEstimatedCostUsd { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
