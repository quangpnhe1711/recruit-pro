using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Services;
using RecruitPro.Infrastructure.Repositories;

namespace RecruitPro.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection
        AddInfrastructureServices(
        this IServiceCollection services)
        {
            services.AddScoped<
                IUserRepository,
                UserRepository>();

            return services;
        }
    }
}
