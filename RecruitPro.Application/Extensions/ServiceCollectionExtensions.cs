using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecruitPro.Application.Extensions
{
    public static class ServiceCollectionExtensions
    {
      public static IServiceCollection
      AddApplicationBusinessLogicServices(
      this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ICandidateProfileService, CandidateProfileService>();
            services.AddScoped<IJobService, JobService>();

            return services;
        }
    }
}
