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
            object? validator = _serviceProvider.GetService(validatorType);
            if (validator == null)
            {
                continue;
            }

            var validationContextType = typeof(ValidationContext<>).MakeGenericType(argument.GetType());
            object validationContext = Activator.CreateInstance(validationContextType, argument)
                ?? throw new InvalidOperationException($"Unable to create validation context for {argument.GetType().Name}.");

            var validateAsyncMethod = validatorType.GetMethod(nameof(IValidator.ValidateAsync), [typeof(IValidationContext), typeof(CancellationToken)]);
            if (validateAsyncMethod == null)
            {
                continue;
            }

            var validationTask = (Task<ValidationResult>)validateAsyncMethod.Invoke(validator, [validationContext, context.HttpContext.RequestAborted])!;
            ValidationResult validationResult = await validationTask;

            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }
        }

        await next();
    }
}
