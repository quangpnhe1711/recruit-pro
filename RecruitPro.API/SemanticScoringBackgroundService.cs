using RecruitPro.Application.Interfaces;

public class SemanticScoringBackgroundService : BackgroundService
{
    private readonly IApplicationSemanticProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SemanticScoringBackgroundService> _logger;

    public SemanticScoringBackgroundService(
        IApplicationSemanticProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<SemanticScoringBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Guid applicationId = await _queue.DequeueAsync(stoppingToken);
                using IServiceScope scope = _scopeFactory.CreateScope();
                IApplicationSemanticScoringService scoringService = scope.ServiceProvider.GetRequiredService<IApplicationSemanticScoringService>();
                await scoringService.ProcessAsync(applicationId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unhandled error in semantic scoring worker.");
            }
        }
    }
}
