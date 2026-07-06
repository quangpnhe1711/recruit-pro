using System.Text.Json;
using RecruitPro.Application.DTOs.Response.Ai;
using RecruitPro.Domain.Constants;

namespace RecruitPro.Application.Common;

/// <summary>
/// v5.2 — pure aggregation of AI telemetry rows into dashboard metrics. Kept free of EF/DB so it is
/// unit-testable in isolation (totals, rates, p50/p95, breakdowns). The service supplies the rows and
/// stitches on recent-failures + echoed filters.
/// </summary>
public static class AiMetricsCalculator
{
    /// <summary>Below this many runs the summary is flagged low-volume so the UI can warn that rates may be unstable.</summary>
    public const int LowVolumeThreshold = 20;

    public static AiOperationsMetricsResponse Compute(IReadOnlyList<AiTelemetryRow> rows)
    {
        AiOperationsMetricsResponse response = new()
        {
            Summary = BuildSummary(rows),
            Latency = BuildLatency(rows),
            DailyTrend = BuildDailyTrend(rows),
            FeatureBreakdown = BuildFeatureBreakdown(rows),
            ProviderBreakdown = BuildProviderBreakdown(rows),
            Errors = BuildErrors(rows),
            RiskSummary = BuildRiskSummary(rows),
        };
        return response;
    }

    private static AiMetricsSummaryDto BuildSummary(IReadOnlyList<AiTelemetryRow> rows)
    {
        int total = rows.Count;
        if (total == 0)
        {
            return new AiMetricsSummaryDto { LowVolume = true };
        }

        int success = rows.Count(r => r.Success);
        int failure = total - success;
        int fallback = rows.Count(r => r.FallbackUsed);
        int schemaChecked = rows.Count(r => r.SchemaValid.HasValue);
        int schemaValid = rows.Count(r => r.SchemaValid == true);
        int schemaFail = rows.Count(r => r.SchemaValid == false);

        return new AiMetricsSummaryDto
        {
            TotalRuns = total,
            SuccessCount = success,
            FailureCount = failure,
            SuccessRate = Rate(success, total),
            FailureRate = Rate(failure, total),
            FallbackCount = fallback,
            FallbackRate = Rate(fallback, total),
            SchemaValidCount = schemaValid,
            SchemaFailCount = schemaFail,
            // Schema-fail rate is over rows that actually returned structured JSON (schema was checked).
            SchemaFailRate = Rate(schemaFail, schemaChecked),
            AvgLatencyMs = Math.Round(rows.Average(r => (double)r.LatencyMs), 1),
            TotalEstimatedCostUsd = rows.Sum(r => r.EstimatedCostUsd ?? 0m),
            TotalPromptTokens = rows.Sum(r => (long)(r.PromptTokens ?? 0)),
            TotalCompletionTokens = rows.Sum(r => (long)(r.CompletionTokens ?? 0)),
            TotalTokens = rows.Sum(r => (long)(r.TotalTokens ?? 0)),
            LowVolume = total < LowVolumeThreshold,
        };
    }

    private static AiLatencyDto BuildLatency(IReadOnlyList<AiTelemetryRow> rows)
    {
        if (rows.Count == 0)
        {
            return new AiLatencyDto();
        }

        int[] sorted = rows.Select(r => r.LatencyMs).OrderBy(v => v).ToArray();
        return new AiLatencyDto
        {
            AvgMs = Math.Round(sorted.Average(), 1),
            P50Ms = Percentile(sorted, 50),
            P95Ms = Percentile(sorted, 95),
        };
    }

    private static List<AiDailyTrendPointDto> BuildDailyTrend(IReadOnlyList<AiTelemetryRow> rows)
        => rows
            .GroupBy(r => r.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new AiDailyTrendPointDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Runs = g.Count(),
                Success = g.Count(r => r.Success),
                Failure = g.Count(r => !r.Success),
                Fallback = g.Count(r => r.FallbackUsed),
                EstimatedCostUsd = g.Sum(r => r.EstimatedCostUsd ?? 0m),
                TotalTokens = g.Sum(r => (long)(r.TotalTokens ?? 0)),
            })
            .ToList();

    private static List<AiFeatureBreakdownDto> BuildFeatureBreakdown(IReadOnlyList<AiTelemetryRow> rows)
        => rows
            .GroupBy(r => r.Feature)
            .OrderByDescending(g => g.Count())
            .Select(g => new AiFeatureBreakdownDto
            {
                Feature = g.Key,
                Runs = g.Count(),
                SuccessRate = Rate(g.Count(r => r.Success), g.Count()),
                FallbackRate = Rate(g.Count(r => r.FallbackUsed), g.Count()),
                AvgLatencyMs = Math.Round(g.Average(r => (double)r.LatencyMs), 1),
                EstimatedCostUsd = g.Sum(r => r.EstimatedCostUsd ?? 0m),
            })
            .ToList();

    private static List<AiProviderBreakdownDto> BuildProviderBreakdown(IReadOnlyList<AiTelemetryRow> rows)
        => rows
            .GroupBy(r => new { Provider = r.ProviderName ?? "unknown", Model = r.ModelName ?? "unknown" })
            .OrderByDescending(g => g.Count())
            .Select(g => new AiProviderBreakdownDto
            {
                Provider = g.Key.Provider,
                Model = g.Key.Model,
                Runs = g.Count(),
                SuccessRate = Rate(g.Count(r => r.Success), g.Count()),
                AvgLatencyMs = Math.Round(g.Average(r => (double)r.LatencyMs), 1),
                EstimatedCostUsd = g.Sum(r => r.EstimatedCostUsd ?? 0m),
            })
            .ToList();

    private static List<AiErrorBucketDto> BuildErrors(IReadOnlyList<AiTelemetryRow> rows)
        => rows
            .Where(r => !r.Success && !string.IsNullOrWhiteSpace(r.ErrorCode))
            .GroupBy(r => r.ErrorCode!)
            .OrderByDescending(g => g.Count())
            .Select(g => new AiErrorBucketDto { ErrorCode = g.Key, Count = g.Count() })
            .ToList();

    private static List<AiRiskBucketDto> BuildRiskSummary(IReadOnlyList<AiTelemetryRow> rows)
    {
        Dictionary<string, int> counts = new();
        foreach (AiTelemetryRow row in rows)
        {
            foreach (string flag in ParseRiskFlags(row.RiskFlagsJson))
            {
                counts[flag] = counts.GetValueOrDefault(flag) + 1;
            }
        }

        return counts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new AiRiskBucketDto { Flag = kv.Key, Severity = AiRiskFlags.Severity(kv.Key), Count = kv.Value })
            .ToList();
    }

    /// <summary>Parses a jsonb string array of risk-flag codes; never throws.</summary>
    public static IReadOnlyList<string> ParseRiskFlags(string? riskFlagsJson)
    {
        if (string.IsNullOrWhiteSpace(riskFlagsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(riskFlagsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Nearest-rank percentile over an ascending-sorted array.</summary>
    public static int Percentile(int[] sortedAscending, int percentile)
    {
        if (sortedAscending.Length == 0)
        {
            return 0;
        }

        if (sortedAscending.Length == 1)
        {
            return sortedAscending[0];
        }

        int rank = (int)Math.Ceiling(percentile / 100.0 * sortedAscending.Length);
        int index = Math.Clamp(rank - 1, 0, sortedAscending.Length - 1);
        return sortedAscending[index];
    }

    private static double Rate(int numerator, int denominator)
        => denominator == 0 ? 0 : Math.Round(numerator / (double)denominator, 4);
}
