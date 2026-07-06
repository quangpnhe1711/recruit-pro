using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Common;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Infrastructure.Service;

/// <summary>
/// v5.1 — the enqueue side of AI telemetry. Singleton holding a bounded write-behind channel; the
/// <see cref="AiTelemetryWriteBehindBackgroundService"/> (in the API project) drains it and persists in
/// batches. RecordAsync maps the record to an entity, estimates cost from tokens, serializes risk flags
/// and metadata to jsonb, then does a NON-BLOCKING TryWrite. It never throws and never blocks the caller;
/// when the queue is full the newest record is dropped and a throttled warning is logged, so a telemetry
/// backlog can never slow down or break the ATS flow that triggered the AI call.
/// </summary>
public class AiTelemetryService : IAiTelemetryService
{
    private readonly Channel<AiRunTelemetry> _channel;
    private readonly AiTelemetrySettings _settings;
    private readonly ILogger<AiTelemetryService> _logger;
    private long _dropped;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AiTelemetryService(IOptions<AiTelemetrySettings> options, ILogger<AiTelemetryService> logger)
    {
        _settings = options.Value;
        _logger = logger;
        int capacity = _settings.MaxQueueSize > 0 ? _settings.MaxQueueSize : 5000;

        // Wait mode + TryWrite gives a detectable, non-blocking drop: TryWrite returns false when full,
        // so we drop the NEWEST record and log rather than ever blocking the request thread.
        // ponytail: single drop-newest policy is the only safe option for a non-blocking writer; the
        // DropWhenQueueFull flag is retained as documented config for a future DropOldest variant.
        _channel = Channel.CreateBounded<AiRunTelemetry>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>Reader consumed by the write-behind background worker.</summary>
    public ChannelReader<AiRunTelemetry> Reader => _channel.Reader;

    public bool Enabled => _settings.Enabled;

    public int BatchSize => _settings.BatchSize > 0 ? _settings.BatchSize : 100;

    public int FlushIntervalSeconds => _settings.FlushIntervalSeconds > 0 ? _settings.FlushIntervalSeconds : 5;

    public ValueTask RecordAsync(AiTelemetryRecord record, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            AiRunTelemetry entity = Map(record);
            if (!_channel.Writer.TryWrite(entity))
            {
                long dropped = Interlocked.Increment(ref _dropped);
                // Throttle: log on the 1st drop and then every 100th so a saturated queue can't spam logs.
                if (dropped == 1 || dropped % 100 == 0)
                {
                    _logger.LogWarning(
                        "AI telemetry queue full; dropped {DroppedCount} record(s) so far (feature {Feature}). Increase AiTelemetry:MaxQueueSize if this persists.",
                        dropped, record.Feature);
                }
            }
        }
        catch (Exception ex)
        {
            // Telemetry must never surface to the caller. Swallow and log.
            _logger.LogWarning(ex, "Failed to enqueue AI telemetry for feature {Feature}.", record.Feature);
        }

        return ValueTask.CompletedTask;
    }

    private AiRunTelemetry Map(AiTelemetryRecord r)
    {
        (decimal? cost, bool estimated) = EstimateCost(r.ModelName, r.PromptTokens, r.CompletionTokens);

        return new AiRunTelemetry
        {
            Feature = r.Feature,
            ProviderName = Truncate(r.ProviderName, 100),
            ModelName = Truncate(r.ModelName, 150),
            PromptVersionId = r.PromptVersionId,
            PromptTokens = r.PromptTokens,
            CompletionTokens = r.CompletionTokens,
            TotalTokens = r.TotalTokens ?? (r.PromptTokens.HasValue || r.CompletionTokens.HasValue
                ? (r.PromptTokens ?? 0) + (r.CompletionTokens ?? 0)
                : null),
            EstimatedCostUsd = cost,
            IsCostEstimated = estimated,
            LatencyMs = r.LatencyMs < 0 ? 0 : r.LatencyMs,
            Success = r.Success,
            FallbackUsed = r.FallbackUsed,
            SchemaValid = r.SchemaValid,
            ErrorCode = Truncate(r.ErrorCode, 100),
            ErrorMessage = Truncate(r.ErrorMessage, 500),
            CorrelationId = Truncate(r.CorrelationId, 100),
            UserId = r.UserId,
            WorkflowExecutionId = r.WorkflowExecutionId,
            RiskFlagsJson = SerializeRiskFlags(r.RiskFlags),
            MetadataJson = SerializeMetadata(r.Metadata),
            CreatedAt = DbDateTime.Now,
        };
    }

    /// <summary>
    /// Estimates USD cost from tokens using the configured pricing table. Returns (null, true) when there
    /// are no tokens or no pricing for the model — cost is always flagged estimated because the provider
    /// never bills a per-call amount back to us.
    /// </summary>
    private (decimal? Cost, bool Estimated) EstimateCost(string? model, int? promptTokens, int? completionTokens)
    {
        if (string.IsNullOrWhiteSpace(model) || (promptTokens is null && completionTokens is null))
        {
            return (null, true);
        }

        if (!_settings.Pricing.TryGetValue(model, out AiModelPricing? pricing) || pricing is null)
        {
            return (null, true);
        }

        decimal cost = ((promptTokens ?? 0) / 1_000_000m) * pricing.InputPerMillionTokensUsd
                     + ((completionTokens ?? 0) / 1_000_000m) * pricing.OutputPerMillionTokensUsd;
        return (Math.Round(cost, 8), true);
    }

    private static string? SerializeRiskFlags(IReadOnlyList<string>? flags)
        => flags is null || flags.Count == 0 ? null : JsonSerializer.Serialize(flags, JsonOptions);

    private static string? SerializeMetadata(IReadOnlyDictionary<string, object?>? metadata)
        => metadata is null || metadata.Count == 0 ? null : JsonSerializer.Serialize(metadata, JsonOptions);

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= max ? value : value[..max];
    }
}
