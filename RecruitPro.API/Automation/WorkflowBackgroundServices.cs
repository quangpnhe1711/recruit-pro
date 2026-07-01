using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;

namespace RecruitPro.API.Automation;

/// <summary>Drains pending outbox events and runs matching workflows. Restart-safe (events are durable).</summary>
public class WorkflowDispatcherBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowDispatcherBackgroundService> _logger;

    public WorkflowDispatcherBackgroundService(IServiceScopeFactory scopeFactory, ILogger<WorkflowDispatcherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IEventOutboxRepository outbox = scope.ServiceProvider.GetRequiredService<IEventOutboxRepository>();
                IWorkflowEngine engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();

                IReadOnlyList<PublishedDomainEvent> events = await outbox.GetDispatchableAsync(DateTime.Now, 25);
                foreach (PublishedDomainEvent domainEvent in events)
                {
                    await engine.ProcessEventAsync(domainEvent.Id, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow dispatcher loop error.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

/// <summary>Retries failed executions whose next_retry_at is due.</summary>
public class WorkflowRetryBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowRetryBackgroundService> _logger;

    public WorkflowRetryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<WorkflowRetryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IWorkflowRetryService retry = scope.ServiceProvider.GetRequiredService<IWorkflowRetryService>();
                await retry.ProcessDueRetriesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow retry loop error.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

/// <summary>Periodically scans for overdue ManagerReview applications and publishes HeadReviewOverdue.</summary>
public class HeadReviewOverdueSchedulerBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HeadReviewOverdueSchedulerBackgroundService> _logger;

    public HeadReviewOverdueSchedulerBackgroundService(IServiceScopeFactory scopeFactory, ILogger<HeadReviewOverdueSchedulerBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IHeadReviewOverdueScanner scanner = scope.ServiceProvider.GetRequiredService<IHeadReviewOverdueScanner>();
                int published = await scanner.ScanAsync(stoppingToken);
                if (published > 0)
                {
                    _logger.LogInformation("HeadReviewOverdue scan published {Count} event(s).", published);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HeadReviewOverdue scheduler loop error.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

/// <summary>Seeds the default workflow templates on startup (idempotent, best-effort).</summary>
public class WorkflowSeederHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowSeederHostedService> _logger;

    public WorkflowSeederHostedService(IServiceScopeFactory scopeFactory, ILogger<WorkflowSeederHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IWorkflowTemplateSeeder seeder = scope.ServiceProvider.GetRequiredService<IWorkflowTemplateSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Best-effort: never block startup if the DB is not yet provisioned.
            _logger.LogWarning(ex, "Workflow template seeding skipped.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
