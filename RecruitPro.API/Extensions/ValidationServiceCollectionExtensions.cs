using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.Validators;

namespace RecruitPro.API.Extensions;

public static class ValidationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationValidators(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateJobRequestValidator>();
        return services;
    }
}
