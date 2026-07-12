using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RecruitPro.API.Filters;

public class ValidationActionFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationActionFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (object? argument in context.ActionArguments.Values)
        {
            if (argument == null)
            {
                continue;
            }

            Type validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            // The DI-registered FluentValidation validator implements both IValidator<T> and the
            // non-generic IValidator. Call the non-generic ValidateAsync(IValidationContext, …) overload
            // directly. (Previously this resolved the overload via reflection on IValidator<T>, where
            // Type.GetMethod does not surface the base-interface member and returned null — so the filter
            // silently skipped validation for EVERY request.)
            if (_serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            Type validationContextType = typeof(ValidationContext<>).MakeGenericType(argument.GetType());
            IValidationContext validationContext = (IValidationContext)(Activator.CreateInstance(validationContextType, argument)
                ?? throw new InvalidOperationException($"Unable to create validation context for {argument.GetType().Name}."));

            ValidationResult validationResult = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }
        }

        await next();
    }
}
