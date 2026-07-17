using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Configurations;
using RecruitPro.Infrastructure.Repositories;
using RecruitPro.Infrastructure.Service;
using System;

namespace RecruitPro.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICandidateProfileRepository, CandidateProfileRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IOfferRepository, OfferRepository>();
        services.AddScoped<IInterviewRepository, InterviewRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ISkillRepository, SkillRepository>();
        services.AddScoped<ICopilotRepository, CopilotRepository>();
        services.AddScoped<IRbacRepository, RbacRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // v4 Workflow Automation + MCP
        services.AddScoped<IEventOutboxRepository, EventOutboxRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IMcpToolAuditRepository, McpToolAuditRepository>();
        services.AddScoped<RecruitPro.Application.Interfaces.IServices.Automation.IHeadReviewOverdueScanner,
            RecruitPro.Infrastructure.Service.Automation.HeadReviewOverdueScanner>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddSingleton<IResumeTextExtractor, PdfResumeTextExtractor>();
        services.AddSingleton<IApplicationSemanticProcessingQueue, InMemoryApplicationSemanticProcessingQueue>();
        services.AddSingleton<IEmbeddingCache, InMemoryEmbeddingCache>();

        // v5.1 AI telemetry: singleton write-behind queue (enqueue side) + scoped batch-write repository.
        // The queue is exposed both as itself (for the write-behind worker) and as IAiTelemetryService.
        services.AddScoped<IAiTelemetryRepository, AiTelemetryRepository>();
        services.AddSingleton<AiTelemetryService>();
        services.AddSingleton<IAiTelemetryService>(sp => sp.GetRequiredService<AiTelemetryService>());

        // AI providers are registered as CONCRETE typed HttpClient clients; each interface then resolves
        // to a telemetry decorator (v5.1) that wraps the concrete provider. The decorator preserves the
        // provider's null-means-fallback contract and never lets a telemetry failure reach the caller.
        services.AddHttpClient<AiResumeParserProvider>(ConfigureAiHttpClient);
        services.AddHttpClient<AiEmbeddingProvider>(ConfigureAiHttpClient);
        services.AddHttpClient<AiCopilotProvider>(ConfigureAiHttpClient);

        services.AddTransient<IResumeParsingAiProvider>(sp => new TelemetryResumeParsingAiProvider(
            sp.GetRequiredService<AiResumeParserProvider>(),
            sp.GetRequiredService<IAiTelemetryService>(),
            sp.GetRequiredService<IHttpContextAccessor>(),
            sp.GetRequiredService<IOptions<AiProviderSettings>>(),
            sp.GetRequiredService<ILogger<TelemetryResumeParsingAiProvider>>()));
        services.AddTransient<IEmbeddingProvider>(sp => new TelemetryEmbeddingProvider(
            sp.GetRequiredService<AiEmbeddingProvider>(),
            sp.GetRequiredService<IAiTelemetryService>(),
            sp.GetRequiredService<IHttpContextAccessor>(),
            sp.GetRequiredService<IOptions<AiProviderSettings>>(),
            sp.GetRequiredService<ILogger<TelemetryEmbeddingProvider>>()));
        services.AddTransient<IAiCopilotProvider>(sp => new TelemetryAiCopilotProvider(
            sp.GetRequiredService<AiCopilotProvider>(),
            sp.GetRequiredService<IAiTelemetryService>(),
            sp.GetRequiredService<IHttpContextAccessor>(),
            sp.GetRequiredService<IOptions<AiProviderSettings>>(),
            sp.GetRequiredService<ILogger<TelemetryAiCopilotProvider>>()));
        services.AddSingleton<IMinioClient>(serviceProvider =>
        {
            MinioSettings settings = serviceProvider.GetRequiredService<IOptions<MinioSettings>>().Value;
            return new MinioClient()
                .WithEndpoint(settings.Endpoint)
                .WithCredentials(settings.AccessKey, settings.SecretKey)
                .WithSSL(settings.UseSsl)
                .Build();
        });
        services.AddSingleton<IFileStorageService, MinioFileStorageService>();
        services.AddSingleton<IEmailService>(serviceProvider =>
        {
            SmtpSettings settings = serviceProvider.GetRequiredService<IOptions<SmtpSettings>>().Value;
            return settings.Enabled
                ? ActivatorUtilities.CreateInstance<SmtpEmailService>(serviceProvider)
                : ActivatorUtilities.CreateInstance<LoggingEmailService>(serviceProvider);
        });

        return services;
    }

    private static void ConfigureAiHttpClient(IServiceProvider serviceProvider, HttpClient client)
    {
        AiProviderSettings settings = serviceProvider.GetRequiredService<IOptions<AiProviderSettings>>().Value;
        client.Timeout = TimeSpan.FromSeconds(Math.Max(30, settings.RequestTimeoutSeconds));
    }
}
