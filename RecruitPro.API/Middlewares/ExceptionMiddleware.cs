using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Exceptions;

namespace RecruitPro.API.Middlewares
{
    /// <summary>
    /// Terminal error boundary for thrown exceptions. Resolves every response through the code-first
    /// contract (<see cref="ApiResponse{T}.ResolveError"/> + <see cref="IErrorMessageProvider"/>) so the
    /// message is never hardcoded here and the nested <c>error</c> block is always populated. Raw exception
    /// text (SQL, connection strings, stack traces) is logged server-side but never sent in Production.
    /// </summary>
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _environment;
        private readonly IErrorMessageProvider _messageProvider;

        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger,
            IHostEnvironment environment,
            IErrorMessageProvider messageProvider)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
            _messageProvider = messageProvider;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                string traceId = Activity.Current?.Id ?? context.TraceIdentifier;
                string rootCause = GetInnermostMessage(ex);

                _logger.LogError(
                    ex,
                    "Unhandled exception. TraceId: {TraceId}. Message: {Message}. RootCause: {RootCause}",
                    traceId,
                    ex.Message,
                    rootCause);

                await HandleExceptionAsync(context, ex, traceId);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception, string traceId)
        {
            context.Response.ContentType = "application/json";

            ApiResponse<object> response = BuildResponse(exception, traceId);
            response.ResolveError(_messageProvider, traceId, BuildDebugExtra(exception, traceId));

            context.Response.StatusCode = response.StatusCode;
            await context.Response.WriteAsJsonAsync(response);
        }

        private ApiResponse<object> BuildResponse(Exception exception, string traceId)
        {
            switch (exception)
            {
                case BusinessAppException business:
                    return ApiResponse<object>.Fail(
                        business.StatusCode,
                        business.Code,
                        business.Params,
                        business.FieldErrors,
                        business.GlobalErrors);

                case ValidationException validation:
                    return ApiResponse<object>.Fail(
                        400,
                        ErrorCodes.ValidationFailed,
                        fieldErrors: MapValidationErrors(validation.Errors));

                case BaseException legacy:
                    // Legacy NotFound/Unauthorize exceptions carry curated (safe) Vietnamese copy but no
                    // code. Keep the copy as the debug message; ResolveError derives the code from status.
                    ApiResponse<object> mapped = ApiResponse<object>.Fail(legacy.StatusCode, DeriveCode(legacy.StatusCode));
                    mapped.Message = legacy.Message;
                    return mapped;

                default:
                    return ApiResponse<object>.Fail(500, ErrorCodes.ServerError);
            }
        }

        private object? BuildDebugExtra(Exception exception, string traceId)
        {
            if (exception is BaseException)
            {
                return null; // known business/validation outcome — no diagnostic payload needed
            }

            // Unhandled 500: full detail in Development only; traceId-only in Production so the raw
            // exception (SQL, connection strings, file paths) never leaves the server.
            return _environment.IsDevelopment()
                ? new
                {
                    traceId,
                    exception = exception.GetType().Name,
                    innerException = exception.InnerException?.Message,
                    rootCause = GetInnermostMessage(exception),
                }
                : new { traceId };
        }

        /// <summary>Map FluentValidation failures to code-first field errors (no hardcoded copy).</summary>
        private static List<ApiFieldErrorInput> MapValidationErrors(IEnumerable<ValidationFailure> failures)
        {
            return failures.Select(failure => new ApiFieldErrorInput
            {
                Field = ToCamelPath(failure.PropertyName),
                Code = MapValidationCode(failure.ErrorCode),
                Params = ExtractParams(failure),
            }).ToList();
        }

        private static IReadOnlyDictionary<string, object?>? ExtractParams(ValidationFailure failure)
        {
            if (failure.FormattedMessagePlaceholderValues is not { Count: > 0 } placeholders)
            {
                return null;
            }

            Dictionary<string, object?> result = new();
            foreach (KeyValuePair<string, object> kv in placeholders)
            {
                // Skip the reflection-y placeholders the FE never needs; keep numeric/limit values.
                if (kv.Key is "PropertyName" or "PropertyValue" or "CollectionIndex" or "PropertyPath")
                {
                    continue;
                }
                result[ToCamelSegment(kv.Key)] = kv.Value;
            }
            return result.Count > 0 ? result : null;
        }

        private static string MapValidationCode(string? fluentErrorCode) => fluentErrorCode switch
        {
            null or "" => ErrorCodes.ValidationFailed,
            "NotEmptyValidator" or "NotNullValidator" => ErrorCodes.Required,
            "EmailValidator" or "AspNetCoreCompatibleEmailValidator" => ErrorCodes.InvalidEmail,
            "MinimumLengthValidator" => ErrorCodes.MinLengthRequired,
            "MaximumLengthValidator" or "LengthValidator" or "ExactLengthValidator" => ErrorCodes.MaxLengthExceeded,
            "RegularExpressionValidator" => ErrorCodes.InvalidFormat,
            "GreaterThanValidator" or "GreaterThanOrEqualValidator"
                or "LessThanValidator" or "LessThanOrEqualValidator"
                or "InclusiveBetweenValidator" or "ExclusiveBetweenValidator" => ErrorCodes.OutOfRange,
            // A validator that set .WithErrorCode(ErrorCodes.X) already carries our UPPER_SNAKE code.
            _ => LooksLikeCode(fluentErrorCode!) ? fluentErrorCode! : ErrorCodes.ValidationFailed,
        };

        private static bool LooksLikeCode(string value)
            => value.Length > 0 && value.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_');

        private static string DeriveCode(int status) => status switch
        {
            400 => ErrorCodes.InvalidInput,
            401 => ErrorCodes.Unauthenticated,
            403 => ErrorCodes.Forbidden,
            404 => ErrorCodes.EntityNotFound,
            409 => ErrorCodes.Conflict,
            422 => ErrorCodes.BusinessRuleViolation,
            _ => ErrorCodes.ServerError,
        };

        private static string ToCamelPath(string propertyName)
            => string.IsNullOrEmpty(propertyName)
                ? propertyName
                : string.Join('.', propertyName.Split('.').Select(ToCamelSegment));

        private static string ToCamelSegment(string segment)
            => string.IsNullOrEmpty(segment) || char.IsLower(segment[0])
                ? segment
                : char.ToLowerInvariant(segment[0]) + segment[1..];

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
