using System.Threading.Channels;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;
using RecruitPro.Infrastructure.Service;

namespace RecruitPro.API;

/// <summary>
/// v5.1 — drains the AI telemetry write-behind channel and persists rows in batches. Coalesces bursts up
/// to <c>BatchSize</c> or <c>FlushIntervalSeconds</c>, whichever comes first. Resolves the scoped
/// repository per batch via <see cref="IServiceScopeFactory"/> (this worker is a singleton). A write
/// failure is retried once then dropped with a logged error — it never crashes the loop. On shutdown it
/// best-effort drains whatever is still queued so in-flight telemetry is not silently lost.
/// </summary>
public class AiTelemetryWriteBehindBackgroundService : BackgroundService
{
    private readonly AiTelemetryService _telemetry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiTelemetryWriteBehindBackgroundService> _logger;

    public AiTelemetryWriteBehindBackgroundService(
        AiTelemetryService telemetry,
        IServiceScopeFactory scopeFactory,
        ILogger<AiTelemetryWriteBehindBackgroundService> logger)
    {
        _telemetry = telemetry;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_telemetry.Enabled)
        {
            _logger.LogInformation("AI telemetry disabled; write-behind worker idle.");
            return;
        }

        ChannelReader<AiRunTelemetry> reader = _telemetry.Reader;
        int batchSize = _telemetry.BatchSize;
        TimeSpan flushInterval = TimeSpan.FromSeconds(_telemetry.FlushIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Block until at least one record is available (or the channel completes / we stop).
                if (!await reader.WaitToReadAsync(stoppingToken))
                {
                    break;
                }

                List<AiRunTelemetry> batch = new(batchSize);
                using CancellationTokenSource flushCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                flushCts.CancelAfter(flushInterval);

                try
                {
                    while (batch.Count < batchSize)
                    {
                        if (reader.TryRead(out AiRunTelemetry? item))
                        {
                            batch.Add(item);
                            continue;
                        }

                        if (!await reader.WaitToReadAsync(flushCts.Token))
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) when (flushCts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                {
                    // Flush interval elapsed — persist whatever we have so far.
                }

                if (batch.Count > 0)
                {
                    await FlushAsync(batch, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unhandled error in AI telemetry write-behind worker.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        await DrainOnShutdownAsync(reader);
    }

    private async Task FlushAsync(List<AiRunTelemetry> batch, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IAiTelemetryRepository repository = scope.ServiceProvider.GetRequiredService<IAiTelemetryRepository>();
                await repository.AddRangeAsync(batch, cancellationToken);
                return;
            }
            catch (Exception exception) when (attempt == 1 && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "AI telemetry batch write failed (attempt {Attempt}); retrying {Count} row(s).", attempt, batch.Count);
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "AI telemetry batch write failed permanently; dropping {Count} row(s).", batch.Count);
                return;
            }
        }
    }

    private async Task DrainOnShutdownAsync(ChannelReader<AiRunTelemetry> reader)
    {
        List<AiRunTelemetry> remaining = new();
        while (reader.TryRead(out AiRunTelemetry? item))
        {
            remaining.Add(item);
        }

        if (remaining.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Flushing {Count} pending AI telemetry row(s) on shutdown.", remaining.Count);
        using CancellationTokenSource shutdownCts = new(TimeSpan.FromSeconds(5));
        await FlushAsync(remaining, shutdownCts.Token);
    }
}
