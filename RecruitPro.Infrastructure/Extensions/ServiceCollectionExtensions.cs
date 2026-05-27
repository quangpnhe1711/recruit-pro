using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Services;
using RecruitPro.Infrastructure.Repositories;
using RecruitPro.Infrastructure.Service;

namespace RecruitPro.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection
        AddInfrastructureServices(
        this IServiceCollection services)
        {
            // register repositories
            services.AddScoped<IUserRepository,UserRepository>();

            // register external services
            services.AddScoped<IJwtService, JwtService>();

            return services;
        }
    }
}
