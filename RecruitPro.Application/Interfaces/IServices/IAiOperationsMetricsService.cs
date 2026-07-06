using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Ai;

namespace RecruitPro.Application.Interfaces.IServices;

/// <summary>v5.2/v5.4 — read-side AI Ops observability (metrics, telemetry list/detail, risk flags).</summary>
public interface IAiOperationsMetricsService
{
    Task<ApiResponse<AiOperationsMetricsResponse>> GetMetricsAsync(
        DateTime? from, DateTime? to, string? feature, string? provider, string? model,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AiTelemetryPageDto>> GetRecentAsync(
        DateTime? from, DateTime? to, string? feature, string? provider, string? model,
        bool? success, bool onlyWithRiskFlags, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<AiTelemetryDetailDto>> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApiResponse<AiRiskFlagsResponse>> GetRiskFlagsAsync(
        DateTime? from, DateTime? to, string? feature, string? provider,
        CancellationToken cancellationToken = default);
}
