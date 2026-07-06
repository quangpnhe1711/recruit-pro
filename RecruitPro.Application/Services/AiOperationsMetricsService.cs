using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Ai;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Services;

/// <summary>
/// v5.2/v5.4 — computes AI Ops dashboard metrics from ai_run_telemetry. Queries a bounded date window
/// (defaults to the last 30 days, capped at 92) and aggregates in <see cref="AiMetricsCalculator"/>.
/// Never returns raw entities and never fabricates numbers: an empty window yields zeroed metrics with
/// LowVolume=true so the UI shows an honest "no / sparse data" state. The rollup upgrade path (a daily
/// pre-aggregate table) can be slotted behind this service without changing the API.
/// </summary>
public class AiOperationsMetricsService : IAiOperationsMetricsService
{
    private const int MaxWindowDays = 92;
    private const int DefaultWindowDays = 30;
    private const int RecentFailuresCount = 10;

    private readonly IAiTelemetryRepository _repository;

    public AiOperationsMetricsService(IAiTelemetryRepository repository) => _repository = repository;

    public async Task<ApiResponse<AiOperationsMetricsResponse>> GetMetricsAsync(
        DateTime? from, DateTime? to, string? feature, string? provider, string? model,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveWindow(from, to, out DateTime start, out DateTime end, out string? error))
        {
            return ApiResponse<AiOperationsMetricsResponse>.BadRequest(error!);
        }

        IReadOnlyList<AiTelemetryRow> rows = await _repository.QueryForMetricsAsync(start, end, feature, provider, model, cancellationToken);
        AiOperationsMetricsResponse response = AiMetricsCalculator.Compute(rows);

        (IReadOnlyList<AiRunTelemetry> failures, _) = await _repository.QueryRecentAsync(
            start, end, feature, provider, model, success: false, onlyWithRiskFlags: false,
            page: 1, pageSize: RecentFailuresCount, cancellationToken);
        response.RecentFailures = failures.Select(ToListItem).ToList();

        response.Filters = BuildFilters(start, end, feature, provider, model);
        return ApiResponse<AiOperationsMetricsResponse>.Ok(response);
    }

    public async Task<ApiResponse<AiTelemetryPageDto>> GetRecentAsync(
        DateTime? from, DateTime? to, string? feature, string? provider, string? model,
        bool? success, bool onlyWithRiskFlags, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        // Recent list uses an open-ended range unless the caller narrows it (defaults are only enforced
        // for the aggregate metrics window).
        (IReadOnlyList<AiRunTelemetry> items, int total) = await _repository.QueryRecentAsync(
            from, to, feature, provider, model, success, onlyWithRiskFlags, page, pageSize, cancellationToken);

        return ApiResponse<AiTelemetryPageDto>.Ok(new AiTelemetryPageDto
        {
            Items = items.Select(ToListItem).ToList(),
            Meta = PaginationMetaBuilder.Build(page, pageSize, total),
        });
    }

    public async Task<ApiResponse<AiTelemetryDetailDto>> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        AiRunTelemetry? entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return ApiResponse<AiTelemetryDetailDto>.NotFound("Không tìm thấy bản ghi telemetry.");
        }

        return ApiResponse<AiTelemetryDetailDto>.Ok(ToDetail(entity));
    }

    public async Task<ApiResponse<AiRiskFlagsResponse>> GetRiskFlagsAsync(
        DateTime? from, DateTime? to, string? feature, string? provider,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveWindow(from, to, out DateTime start, out DateTime end, out string? error))
        {
            return ApiResponse<AiRiskFlagsResponse>.BadRequest(error!);
        }

        IReadOnlyList<AiTelemetryRow> rows = await _repository.QueryForMetricsAsync(start, end, feature, provider, model: null, cancellationToken);

        List<(AiTelemetryRow Row, IReadOnlyList<string> Flags)> flagged = rows
            .Select(r => (Row: r, Flags: AiMetricsCalculator.ParseRiskFlags(r.RiskFlagsJson)))
            .Where(x => x.Flags.Count > 0)
            .ToList();

        AiRiskFlagsResponse response = new()
        {
            TotalRiskyRuns = flagged.Count,
            ByType = flagged
                .SelectMany(x => x.Flags)
                .GroupBy(f => f)
                .OrderByDescending(g => g.Count())
                .Select(g => new AiRiskBucketDto { Flag = g.Key, Severity = AiRiskFlags.Severity(g.Key), Count = g.Count() })
                .ToList(),
            DailyTrend = flagged
                .GroupBy(x => x.Row.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new AiRiskDailyPointDto { Date = g.Key.ToString("yyyy-MM-dd"), Count = g.Count() })
                .ToList(),
            FeatureBreakdown = flagged
                .GroupBy(x => x.Row.Feature)
                .OrderByDescending(g => g.Count())
                .Select(g => new AiRiskFeaturePointDto { Feature = g.Key, Count = g.Count() })
                .ToList(),
            Filters = BuildFilters(start, end, feature, provider, model: null),
        };

        return ApiResponse<AiRiskFlagsResponse>.Ok(response);
    }

    private static bool TryResolveWindow(DateTime? from, DateTime? to, out DateTime start, out DateTime end, out string? error)
    {
        error = null;
        // 'end' is exclusive; add a day so a to-date includes its whole day.
        end = (to ?? DbDateTime.Today).Date.AddDays(1);
        start = (from ?? end.AddDays(-DefaultWindowDays - 1)).Date;

        if (start >= end)
        {
            error = "Khoảng thời gian không hợp lệ: 'from' phải trước 'to'.";
            return false;
        }

        if ((end - start).TotalDays > MaxWindowDays)
        {
            error = $"Khoảng thời gian quá dài (tối đa {MaxWindowDays} ngày).";
            return false;
        }

        return true;
    }

    private static AiMetricsFiltersDto BuildFilters(DateTime start, DateTime end, string? feature, string? provider, string? model)
        => new()
        {
            From = start.ToString("yyyy-MM-dd"),
            To = end.AddDays(-1).ToString("yyyy-MM-dd"),
            Feature = feature,
            Provider = provider,
            Model = model,
            AvailableFeatures = AiFeatureKeys.All.ToList(),
        };

    private static AiTelemetryListItemDto ToListItem(AiRunTelemetry e) => new()
    {
        Id = e.Id,
        Feature = e.Feature,
        ProviderName = e.ProviderName,
        ModelName = e.ModelName,
        Success = e.Success,
        FallbackUsed = e.FallbackUsed,
        SchemaValid = e.SchemaValid,
        LatencyMs = e.LatencyMs,
        EstimatedCostUsd = e.EstimatedCostUsd,
        TotalTokens = e.TotalTokens,
        ErrorCode = e.ErrorCode,
        ErrorMessage = e.ErrorMessage,
        RiskFlags = AiMetricsCalculator.ParseRiskFlags(e.RiskFlagsJson).ToList(),
        CreatedAt = e.CreatedAt,
    };

    private static AiTelemetryDetailDto ToDetail(AiRunTelemetry e) => new()
    {
        Id = e.Id,
        Feature = e.Feature,
        ProviderName = e.ProviderName,
        ModelName = e.ModelName,
        Success = e.Success,
        FallbackUsed = e.FallbackUsed,
        SchemaValid = e.SchemaValid,
        LatencyMs = e.LatencyMs,
        EstimatedCostUsd = e.EstimatedCostUsd,
        TotalTokens = e.TotalTokens,
        ErrorCode = e.ErrorCode,
        ErrorMessage = e.ErrorMessage,
        RiskFlags = AiMetricsCalculator.ParseRiskFlags(e.RiskFlagsJson).ToList(),
        CreatedAt = e.CreatedAt,
        PromptVersionId = e.PromptVersionId,
        PromptTokens = e.PromptTokens,
        CompletionTokens = e.CompletionTokens,
        IsCostEstimated = e.IsCostEstimated,
        CorrelationId = e.CorrelationId,
        UserId = e.UserId,
        WorkflowExecutionId = e.WorkflowExecutionId,
        MetadataJson = e.MetadataJson,
    };
}
