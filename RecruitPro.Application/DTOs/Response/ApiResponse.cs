using System.Linq;
using System.Text.Json.Serialization;
using RecruitPro.Application.Common;

namespace RecruitPro.Application.DTOs.Response
{
    /// <summary>
    /// Non-generic view of an error envelope so the result filter / middleware can resolve messages
    /// without knowing the payload type <c>T</c>.
    /// </summary>
    public interface IErrorEnvelope
    {
        bool Success { get; }
        int StatusCode { get; }
        string? ErrorCode { get; }
        ApiError? ErrorDetail { get; }

        /// <summary>
        /// Fill the flat <c>message</c>/<c>errors</c> AND the nested <c>error</c> block from the stable
        /// code(s) via <paramref name="provider"/>. Idempotent (no-op once <see cref="Error"/> is set, or
        /// on success responses). This is the ONLY place a code becomes a message.
        /// </summary>
        void ResolveError(IErrorMessageProvider provider, string? traceId, object? debugExtra = null);
    }

    public class ApiResponse<T> : IErrorEnvelope
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; }

        /// <summary>Stable machine-readable error code (ERROR-CONTRACT.md). Null on success.</summary>
        public string? ErrorCode { get; set; }
        public T? Data { get; set; }

        /// <summary>Legacy flat field→messages map, kept for back-compat with existing FE consumers.</summary>
        public Dictionary<string, string[]>? Errors { get; set; }
        public object? Extra { get; set; }

        /// <summary>Nested self-describing error block (code-first contract). Null on success. Serialized
        /// as <c>error</c> — the C# name avoids clashing with the static <c>Error(...)</c> factory.</summary>
        [JsonPropertyName("error")]
        public ApiError? ErrorDetail { get; set; }

        // --- Plumbing: what the throw/return site supplied, consumed by ResolveError. Never serialized. ---
        [JsonIgnore] public IReadOnlyDictionary<string, object?>? ErrorParams { get; set; }
        [JsonIgnore] public List<ApiFieldErrorInput>? FieldErrorInputs { get; set; }
        [JsonIgnore] public List<ApiGlobalErrorInput>? GlobalErrorInputs { get; set; }

        private ApiResponse(
            bool success,
            int statusCode,
            string message,
            T? data = default,
            Dictionary<string, string[]>? errors = null,
            object? extra = null,
            string? errorCode = null)
        {
            Success = success;
            StatusCode = statusCode;
            Message = message;
            Data = data;
            Errors = errors;
            Extra = extra;
            ErrorCode = errorCode;
        }

        // ---------------------------------------------------------------- success

        public static ApiResponse<T> Ok(T data, string message = "Thành công")
            => new(true, 200, message, data);

        public static ApiResponse<T> Created(T data, string message = "Tạo thành công")
            => new(true, 201, message, data);

        public static ApiResponse<T> NoContent(string message = "Không có dữ liệu")
            => new(true, 204, message);

        // ---------------------------------------------------------------- errors (code-first)

        /// <summary>Build an error response from a stable code. Message is resolved later by ResolveError.</summary>
        public static ApiResponse<T> Fail(
            int statusCode,
            string code,
            IReadOnlyDictionary<string, object?>? @params = null,
            IEnumerable<ApiFieldErrorInput>? fieldErrors = null,
            IEnumerable<ApiGlobalErrorInput>? globalErrors = null,
            object? extra = null)
            => new(false, statusCode, message: string.Empty, extra: extra, errorCode: code)
            {
                ErrorParams = @params,
                FieldErrorInputs = fieldErrors?.ToList(),
                GlobalErrorInputs = globalErrors?.ToList(),
            };

        public static ApiResponse<T> BadRequest(string code = ErrorCodes.InvalidInput, IReadOnlyDictionary<string, object?>? @params = null)
            => Fail(400, code, @params);

        public static ApiResponse<T> ValidationError(IEnumerable<ApiFieldErrorInput> fieldErrors, string code = ErrorCodes.ValidationFailed)
            => Fail(400, code, fieldErrors: fieldErrors);

        public static ApiResponse<T> Unauthorized(string code = ErrorCodes.Unauthenticated, IReadOnlyDictionary<string, object?>? @params = null)
            => Fail(401, code, @params);

        public static ApiResponse<T> Forbidden(string code = ErrorCodes.Forbidden, IReadOnlyDictionary<string, object?>? @params = null)
            => Fail(403, code, @params);

        public static ApiResponse<T> NotFound(string code = ErrorCodes.EntityNotFound, IReadOnlyDictionary<string, object?>? @params = null)
            => Fail(404, code, @params);

        public static ApiResponse<T> Conflict(string code = ErrorCodes.Conflict, IReadOnlyDictionary<string, object?>? @params = null, object? extra = null)
            => Fail(409, code, @params, extra: extra);

        public static ApiResponse<T> UnprocessableEntity(string code = ErrorCodes.BusinessRuleViolation, IReadOnlyDictionary<string, object?>? @params = null, object? extra = null)
            => Fail(422, code, @params, extra: extra);

        public static ApiResponse<T> Error(string code = ErrorCodes.ServerError, object? extra = null)
            => Fail(500, code, extra: extra);

        // ---------------------------------------------------------------- central resolution

        public void ResolveError(IErrorMessageProvider provider, string? traceId, object? debugExtra = null)
        {
            if (Success || ErrorDetail is not null)
            {
                return;
            }

            string code = string.IsNullOrEmpty(ErrorCode) ? DeriveCodeFromStatus(StatusCode) : ErrorCode!;

            // Straggler guard: a human message left where a code belongs (un-migrated site). Codes are
            // UPPER_SNAKE_CASE; a value with a space or a lowercase letter is legacy copy. Keep it as the
            // debug message, but derive a real code so the FE still branches correctly.
            // ponytail: this exists so a missed call site degrades gracefully instead of shipping garbage.
            string? legacyMessage = null;
            if (code.Length == 0 || code.IndexOf(' ') >= 0 || code.Any(char.IsLower))
            {
                legacyMessage = code;
                code = DeriveCodeFromStatus(StatusCode);
            }
            else if (!string.IsNullOrEmpty(Message))
            {
                legacyMessage = Message; // an explicitly-set debug message wins over the generic template
            }

            List<ApiFieldError> fieldErrors = (FieldErrorInputs ?? new()).Select(f => new ApiFieldError
            {
                Field = f.Field,
                Code = f.Code,
                Params = ToParamDict(f.Params),
                Message = provider.GetMessage(f.Code, f.Params),
            }).ToList();

            List<ApiGlobalError> globalErrors = (GlobalErrorInputs ?? new()).Select(g => new ApiGlobalError
            {
                Code = g.Code,
                Params = ToParamDict(g.Params),
                Message = provider.GetMessage(g.Code, g.Params),
            }).ToList();

            string message = legacyMessage ?? provider.GetMessage(code, ErrorParams);

            ErrorCode = code;
            Message = message;
            if (fieldErrors.Count > 0)
            {
                Errors = fieldErrors
                    .GroupBy(f => f.Field)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());
            }

            ErrorDetail = new ApiError
            {
                Type = DeriveType(StatusCode, code, fieldErrors.Count > 0),
                Code = code,
                Message = message,
                FieldErrors = fieldErrors,
                GlobalErrors = globalErrors,
                TraceId = traceId,
            };

            if (debugExtra is not null)
            {
                Extra = debugExtra;
            }
        }

        private static Dictionary<string, object?> ToParamDict(IReadOnlyDictionary<string, object?>? source)
            => source is null ? new() : new Dictionary<string, object?>(source);

        private static string DeriveCodeFromStatus(int status) => status switch
        {
            400 => ErrorCodes.InvalidInput,
            401 => ErrorCodes.Unauthenticated,
            403 => ErrorCodes.Forbidden,
            404 => ErrorCodes.EntityNotFound,
            409 => ErrorCodes.Conflict,
            422 => ErrorCodes.BusinessRuleViolation,
            _ => ErrorCodes.ServerError,
        };

        private static string DeriveType(int status, string code, bool hasFieldErrors) => status switch
        {
            400 => hasFieldErrors || code is ErrorCodes.ValidationFailed or ErrorCodes.ValidationError or ErrorCodes.FormInvalid
                ? "VALIDATION_ERROR"
                : "BAD_REQUEST",
            401 => "AUTH_ERROR",
            403 => "FORBIDDEN",
            404 => "NOT_FOUND",
            409 => "CONFLICT",
            422 => "BUSINESS_ERROR",
            >= 500 => "SERVER_ERROR",
            _ => "ERROR",
        };
    }
}
