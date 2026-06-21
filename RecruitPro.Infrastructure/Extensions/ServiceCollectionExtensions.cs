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

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddSingleton<IResumeTextExtractor, PdfResumeTextExtractor>();
        services.AddSingleton<IApplicationSemanticProcessingQueue, InMemoryApplicationSemanticProcessingQueue>();
        services.AddSingleton<IEmbeddingCache, InMemoryEmbeddingCache>();
        services.AddHttpClient<IResumeParsingAiProvider, AiResumeParserProvider>((serviceProvider, client) =>
        {
            AiProviderSettings settings = serviceProvider.GetRequiredService<IOptions<AiProviderSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(30, settings.RequestTimeoutSeconds));
        });
        services.AddHttpClient<IEmbeddingProvider, AiEmbeddingProvider>((serviceProvider, client) =>
        {
            AiProviderSettings settings = serviceProvider.GetRequiredService<IOptions<AiProviderSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(30, settings.RequestTimeoutSeconds));
        });
        services.AddHttpClient<IAiCopilotProvider, AiCopilotProvider>((serviceProvider, client) =>
        {
            AiProviderSettings settings = serviceProvider.GetRequiredService<IOptions<AiProviderSettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(30, settings.RequestTimeoutSeconds));
        });
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
        services.AddSingleton<IEmailService, LoggingEmailService>();

        return services;
    }
}
