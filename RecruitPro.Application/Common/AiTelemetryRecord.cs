namespace RecruitPro.Application.Common;

/// <summary>
/// v5.1 — the immutable record a telemetry decorator hands to <c>IAiTelemetryService.RecordAsync</c>.
/// Risk flags and metadata are given as CLR collections; the service serializes them to jsonb and
/// estimates cost from tokens before persisting. Carries metadata/summaries only — never full payloads.
/// </summary>
public sealed class AiTelemetryRecord
{
    public required string Feature { get; init; }
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public Guid? PromptVersionId { get; init; }

    public int? PromptTokens { get; init; }
    public int? CompletionTokens { get; init; }
    public int? TotalTokens { get; init; }

    public int LatencyMs { get; init; }
    public bool Success { get; init; }
    public bool FallbackUsed { get; init; }
    public bool? SchemaValid { get; init; }

    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public string? CorrelationId { get; init; }
    public Guid? UserId { get; init; }
    public Guid? WorkflowExecutionId { get; init; }

    public IReadOnlyList<string>? RiskFlags { get; init; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }
}
