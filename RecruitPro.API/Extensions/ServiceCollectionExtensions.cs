using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Infrastructure.Repositories;

namespace RecruitPro.API.Extensions
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
