using RecruitPro.Application.Common;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories;

/// <summary>v5 — persistence for AI telemetry (batch write from the write-behind worker + read queries).</summary>
public interface IAiTelemetryRepository
{
    /// <summary>Batch-insert telemetry rows. Called only by the write-behind background worker.</summary>
    Task AddRangeAsync(IReadOnlyList<AiRunTelemetry> items, CancellationToken cancellationToken = default);

    /// <summary>Projected rows in [from, to] (inclusive of from, exclusive of to) with optional filters, for metrics.</summary>
    Task<IReadOnlyList<AiTelemetryRow>> QueryForMetricsAsync(
        DateTime from, DateTime to, string? feature, string? provider, string? model,
        CancellationToken cancellationToken = default);

    /// <summary>Recent telemetry rows (newest first) for the runs/failures/risk tables. Paginated.</summary>
    Task<(IReadOnlyList<AiRunTelemetry> Items, int Total)> QueryRecentAsync(
        DateTime? from, DateTime? to, string? feature, string? provider, string? model,
        bool? success, bool onlyWithRiskFlags, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<AiRunTelemetry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
}
