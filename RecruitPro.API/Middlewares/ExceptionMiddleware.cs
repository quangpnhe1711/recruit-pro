using FluentValidation;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Exceptions;

namespace RecruitPro.API.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                string traceId = context.TraceIdentifier;
                string rootCause = GetInnermostMessage(ex);

                _logger.LogError(
                    ex,
                    "Unhandled exception. TraceId: {TraceId}. Message: {Message}. RootCause: {RootCause}",
                    traceId,
                    ex.Message,
                    rootCause);

                await HandleExceptionAsync(context, ex, _environment.IsDevelopment(), traceId);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception, bool isDevelopment, string traceId)
        {
            context.Response.ContentType = "application/json";

            int statusCode;
            ApiResponse<object> response;

            if (exception is BaseException customException)
            {
                statusCode = customException.StatusCode;
                response = statusCode switch
                {
                    400 => ApiResponse<object>.BadRequest(customException.Message),
                    401 => ApiResponse<object>.Unauthorized(customException.Message),
                    404 => ApiResponse<object>.NotFound(customException.Message),
                    _ => ApiResponse<object>.Error(customException.Message)
                };
            }
            else if (exception is ValidationException validationException)
            {
                statusCode = 400;
                Dictionary<string, string[]> errors = validationException.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

                response = ApiResponse<object>.ValidationError("Dữ liệu không hợp lệ.", errors);
            }
            else
            {
                statusCode = 500;
                response = ApiResponse<object>.Error(
                    exception.Message,
                    isDevelopment
                        ? new
                        {
                            traceId,
                            exception = exception.GetType().Name,
                            innerException = exception.InnerException?.Message,
                            rootCause = GetInnermostMessage(exception)
                        }
                        : new { traceId });
            }

            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(response);
        }

        private static string GetInnermostMessage(Exception exception)
        {
            Exception current = exception;
            while (current.InnerException is not null)
            {
                current = current.InnerException;
            }

            return current.Message;
        }
    }
}
