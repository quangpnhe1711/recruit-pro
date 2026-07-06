namespace RecruitPro.Application.Common;

/// <summary>
/// v5.2 — a lightweight projection of an ai_run_telemetry row used for metrics aggregation. Only the
/// columns needed to compute summary/breakdown/latency/error metrics are selected, so a date-range query
/// stays cheap. Aggregation runs in <c>AiMetricsCalculator</c> (pure, unit-testable) over these rows.
/// </summary>
public sealed class AiTelemetryRow
{
    public string Feature { get; init; } = string.Empty;
    public string? ProviderName { get; init; }
    public string? ModelName { get; init; }
    public bool Success { get; init; }
    public bool FallbackUsed { get; init; }
    public bool? SchemaValid { get; init; }
    public int LatencyMs { get; init; }
    public int? PromptTokens { get; init; }
    public int? CompletionTokens { get; init; }
    public int? TotalTokens { get; init; }
    public decimal? EstimatedCostUsd { get; init; }
    public string? ErrorCode { get; init; }
    public string? RiskFlagsJson { get; init; }
    public DateTime CreatedAt { get; init; }
}
