using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.DTOs.Response.Ai;

/// <summary>v5.2 — AI Operations dashboard payload. Aggregated, never raw entities.</summary>
public class AiOperationsMetricsResponse
{
    public AiMetricsSummaryDto Summary { get; set; } = new();
    public List<AiDailyTrendPointDto> DailyTrend { get; set; } = [];
    public List<AiFeatureBreakdownDto> FeatureBreakdown { get; set; } = [];
    public List<AiProviderBreakdownDto> ProviderBreakdown { get; set; } = [];
    public AiLatencyDto Latency { get; set; } = new();
    public List<AiErrorBucketDto> Errors { get; set; } = [];
    public List<AiRiskBucketDto> RiskSummary { get; set; } = [];
    public List<AiTelemetryListItemDto> RecentFailures { get; set; } = [];
    public AiMetricsFiltersDto Filters { get; set; } = new();
}

public class AiMetricsSummaryDto
{
    public int TotalRuns { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double SuccessRate { get; set; }
    public double FailureRate { get; set; }
    public int FallbackCount { get; set; }
    public double FallbackRate { get; set; }
    public int SchemaValidCount { get; set; }
    public int SchemaFailCount { get; set; }
    public double SchemaFailRate { get; set; }
    public double AvgLatencyMs { get; set; }
    public decimal TotalEstimatedCostUsd { get; set; }
    public long TotalPromptTokens { get; set; }
    public long TotalCompletionTokens { get; set; }
    public long TotalTokens { get; set; }

    /// <summary>True when the sample is too small for stable rates (UI shows a "data sparse" note).</summary>
    public bool LowVolume { get; set; }
}

public class AiDailyTrendPointDto
{
    public string Date { get; set; } = string.Empty;
    public int Runs { get; set; }
    public int Success { get; set; }
    public int Failure { get; set; }
    public int Fallback { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public long TotalTokens { get; set; }
}

public class AiFeatureBreakdownDto
{
    public string Feature { get; set; } = string.Empty;
    public int Runs { get; set; }
    public double SuccessRate { get; set; }
    public double FallbackRate { get; set; }
    public double AvgLatencyMs { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}

public class AiProviderBreakdownDto
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Runs { get; set; }
    public double SuccessRate { get; set; }
    public double AvgLatencyMs { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}

public class AiLatencyDto
{
    public double AvgMs { get; set; }
    public int P50Ms { get; set; }
    public int P95Ms { get; set; }
}

public class AiErrorBucketDto
{
    public string ErrorCode { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AiRiskBucketDto
{
    public string Flag { get; set; } = string.Empty;
    public string Severity { get; set; } = "low";
    public int Count { get; set; }
}

public class AiMetricsFiltersDto
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string? Feature { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public List<string> AvailableFeatures { get; set; } = [];
}

/// <summary>Row for the recent-runs / recent-failures / risky-runs tables.</summary>
public class AiTelemetryListItemDto
{
    public Guid Id { get; set; }
    public string Feature { get; set; } = string.Empty;
    public string? ProviderName { get; set; }
    public string? ModelName { get; set; }
    public bool Success { get; set; }
    public bool FallbackUsed { get; set; }
    public bool? SchemaValid { get; set; }
    public int LatencyMs { get; set; }
    public decimal? EstimatedCostUsd { get; set; }
    public int? TotalTokens { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> RiskFlags { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

/// <summary>Full detail for a single telemetry row.</summary>
public class AiTelemetryDetailDto : AiTelemetryListItemDto
{
    public Guid? PromptVersionId { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public bool IsCostEstimated { get; set; }
    public string? CorrelationId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? WorkflowExecutionId { get; set; }
    public string? MetadataJson { get; set; }
}

/// <summary>Paginated telemetry list (Shape A envelope: Items + Meta).</summary>
public class AiTelemetryPageDto
{
    public List<AiTelemetryListItemDto> Items { get; set; } = [];
    public ApiEnvelopeMeta Meta { get; set; } = new();
}

/// <summary>v5.4 — risk-flags dashboard payload.</summary>
public class AiRiskFlagsResponse
{
    public int TotalRiskyRuns { get; set; }
    public List<AiRiskBucketDto> ByType { get; set; } = [];
    public List<AiRiskDailyPointDto> DailyTrend { get; set; } = [];
    public List<AiRiskFeaturePointDto> FeatureBreakdown { get; set; } = [];
    public AiMetricsFiltersDto Filters { get; set; } = new();
}

public class AiRiskDailyPointDto
{
    public string Date { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AiRiskFeaturePointDto
{
    public string Feature { get; set; } = string.Empty;
    public int Count { get; set; }
}
