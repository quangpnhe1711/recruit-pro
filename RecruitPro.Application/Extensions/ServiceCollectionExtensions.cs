using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Services;

namespace RecruitPro.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationBusinessLogicServices(this IServiceCollection services)
    {
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICandidateService, CandidateService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IInterviewService, InterviewService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IManagerAnalyticsService, ManagerAnalyticsService>();
        services.AddScoped<ICopilotService, CopilotService>();
        services.AddScoped<IOfferService, OfferService>();

        return services;
    }
}
