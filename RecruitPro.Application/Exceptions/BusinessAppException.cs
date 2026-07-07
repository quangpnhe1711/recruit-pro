using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Exceptions;

/// <summary>
/// The code-first way to signal a business/validation failure from a service. Carries a stable
/// <see cref="Code"/> (+ optional field/global errors and params); the human message is resolved
/// centrally by IErrorMessageProvider in the exception middleware — never passed here.
/// </summary>
public sealed class BusinessAppException : BaseException
{
    public string Code { get; }
    public IReadOnlyDictionary<string, object?>? Params { get; }
    public IReadOnlyList<ApiFieldErrorInput>? FieldErrors { get; }
    public IReadOnlyList<ApiGlobalErrorInput>? GlobalErrors { get; }

    public BusinessAppException(
        string code,
        int statusCode = 422,
        IReadOnlyDictionary<string, object?>? @params = null,
        IEnumerable<ApiFieldErrorInput>? fieldErrors = null,
        IEnumerable<ApiGlobalErrorInput>? globalErrors = null)
        : base(code, statusCode) // base.Message = code, useful in logs; never shown to users
    {
        Code = code;
        Params = @params;
        FieldErrors = fieldErrors?.ToList();
        GlobalErrors = globalErrors?.ToList();
    }
}
