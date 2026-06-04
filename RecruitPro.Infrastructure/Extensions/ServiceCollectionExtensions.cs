using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Infrastructure.Repositories;
using RecruitPro.Infrastructure.Service;

namespace RecruitPro.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            // register repositories
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ICandidateProfileRepository, CandidateProfileRepository>();
            services.AddScoped<IHrRepository, HrRepository>();
            services.AddScoped<IJobRepository, JobRepository>();

            // register UnitOfWork
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // register external services
            services.AddScoped<IJwtService, JwtService>();

            // file storage
            services.AddSingleton<IFileStorageService, MinioFileStorageService>();

            return services;
        }
    }
}
