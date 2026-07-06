namespace RecruitPro.Domain.Entities;

/// <summary>
/// v5.1 — one row per AI invocation in the system (ranking, fit, resume parse, embedding, chat, ...).
/// Written best-effort via a write-behind queue; a telemetry failure must never break the ATS flow that
/// triggered the AI call. Stores metadata + summaries only — never full CV/prompt/output payloads.
/// </summary>
public class AiRunTelemetry
{
    public Guid Id { get; set; }

    /// <summary>Feature that invoked AI. One of <see cref="Constants.AiFeatureKeys"/>.</summary>
    public string Feature { get; set; } = string.Empty;

    public string? ProviderName { get; set; }
    public string? ModelName { get; set; }

    /// <summary>Active prompt version at call time, when the feature resolves prompts via the registry.</summary>
    public Guid? PromptVersionId { get; set; }

    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }

    public decimal? EstimatedCostUsd { get; set; }

    /// <summary>True when cost was derived from a pricing table rather than a provider-billed amount.</summary>
    public bool IsCostEstimated { get; set; } = true;

    public int LatencyMs { get; set; }
    public bool Success { get; set; }

    /// <summary>True when the deterministic fallback path was taken (provider disabled/failed/unusable).</summary>
    public bool FallbackUsed { get; set; }

    /// <summary>Null when the feature does not return structured JSON; else whether it validated.</summary>
    public bool? SchemaValid { get; set; }

    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public string? CorrelationId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? WorkflowExecutionId { get; set; }

    /// <summary>jsonb array of risk-flag codes (<see cref="Constants.AiRiskFlags"/>), or null.</summary>
    public string? RiskFlagsJson { get; set; }

    /// <summary>jsonb bag of small, non-sensitive context (e.g. jobId, candidateCount). Never PII payloads.</summary>
    public string? MetadataJson { get; set; }

    public DateTime CreatedAt { get; set; }
}
