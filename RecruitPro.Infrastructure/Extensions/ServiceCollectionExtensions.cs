using Microsoft.Extensions.Options;
using Minio;
using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Configurations;
using RecruitPro.Infrastructure.Repositories;
using RecruitPro.Infrastructure.Service;

namespace RecruitPro.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICandidateProfileRepository, CandidateProfileRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IInterviewRepository, InterviewRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ISkillRepository, SkillRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IJwtService, JwtService>();
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

        return services;
    }
}
