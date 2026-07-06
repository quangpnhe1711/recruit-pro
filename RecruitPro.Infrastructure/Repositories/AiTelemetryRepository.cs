using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Common;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories;

public class AiTelemetryRepository : IAiTelemetryRepository
{
    private readonly AppDbContext _context;

    public AiTelemetryRepository(AppDbContext context) => _context = context;

    public async Task AddRangeAsync(IReadOnlyList<AiRunTelemetry> items, CancellationToken cancellationToken = default)
    {
        await _context.AiRunTelemetries.AddRangeAsync(items, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiTelemetryRow>> QueryForMetricsAsync(
        DateTime from, DateTime to, string? feature, string? provider, string? model,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AiRunTelemetry> query = Filter(_context.AiRunTelemetries.AsNoTracking(), from, to, feature, provider, model);

        return await query
            .OrderBy(t => t.CreatedAt)
            .Select(t => new AiTelemetryRow
            {
                Feature = t.Feature,
                ProviderName = t.ProviderName,
                ModelName = t.ModelName,
                Success = t.Success,
                FallbackUsed = t.FallbackUsed,
                SchemaValid = t.SchemaValid,
                LatencyMs = t.LatencyMs,
                PromptTokens = t.PromptTokens,
                CompletionTokens = t.CompletionTokens,
                TotalTokens = t.TotalTokens,
                EstimatedCostUsd = t.EstimatedCostUsd,
                ErrorCode = t.ErrorCode,
                RiskFlagsJson = t.RiskFlagsJson,
                CreatedAt = t.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AiRunTelemetry> Items, int Total)> QueryRecentAsync(
        DateTime? from, DateTime? to, string? feature, string? provider, string? model,
        bool? success, bool onlyWithRiskFlags, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AiRunTelemetry> query = _context.AiRunTelemetries.AsNoTracking();

        if (from.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(t => t.CreatedAt < to.Value);
        }
        if (!string.IsNullOrWhiteSpace(feature))
        {
            query = query.Where(t => t.Feature == feature);
        }
        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(t => t.ProviderName == provider);
        }
        if (!string.IsNullOrWhiteSpace(model))
        {
            query = query.Where(t => t.ModelName == model);
        }
        if (success.HasValue)
        {
            query = query.Where(t => t.Success == success.Value);
        }
        if (onlyWithRiskFlags)
        {
            query = query.Where(t => t.RiskFlagsJson != null);
        }

        int total = await query.CountAsync(cancellationToken);
        List<AiRunTelemetry> items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<AiRunTelemetry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.AiRunTelemetries.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => _context.AiRunTelemetries.AsNoTracking().AnyAsync(cancellationToken);

    private static IQueryable<AiRunTelemetry> Filter(
        IQueryable<AiRunTelemetry> query, DateTime from, DateTime to, string? feature, string? provider, string? model)
    {
        query = query.Where(t => t.CreatedAt >= from && t.CreatedAt < to);
        if (!string.IsNullOrWhiteSpace(feature))
        {
            query = query.Where(t => t.Feature == feature);
        }
        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(t => t.ProviderName == provider);
        }
        if (!string.IsNullOrWhiteSpace(model))
        {
            query = query.Where(t => t.ModelName == model);
        }

        return query;
    }
}
