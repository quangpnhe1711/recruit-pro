namespace RecruitPro.Application.DTOs.Response;

/// <summary>
/// Nested, self-describing error block on an error response (ERROR-CONTRACT.md). Carried alongside the
/// legacy flat fields (message/statusCode/errorCode/errors) for back-compat. The web frontend reads this
/// block, branches on <see cref="Code"/> (+ per-error params), and renders from its own i18n dictionary —
/// it never renders <see cref="Message"/>. <see cref="Message"/> is a debug-friendly payload only.
/// </summary>
public sealed class ApiError
{
    /// <summary>Coarse category derived from HTTP status (VALIDATION_ERROR, NOT_FOUND, BUSINESS_ERROR, …).</summary>
    public string Type { get; init; } = default!;
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
    public List<ApiFieldError> FieldErrors { get; init; } = new();
    public List<ApiGlobalError> GlobalErrors { get; init; } = new();
    public string? TraceId { get; init; }
}

/// <summary>A field-scoped error the frontend maps onto a form input (red field, message under the input).</summary>
public sealed class ApiFieldError
{
    /// <summary>camelCase field name matching the frontend form.</summary>
    public string Field { get; init; } = default!;
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
    public Dictionary<string, object?> Params { get; init; } = new();
}

/// <summary>A form-level (non field-scoped) error the frontend shows as an inline form banner.</summary>
public sealed class ApiGlobalError
{
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
    public Dictionary<string, object?> Params { get; init; } = new();
}

// --- Inputs: what a throw/return site supplies (code + params + field). Message is resolved centrally. ---

public sealed class ApiFieldErrorInput
{
    public required string Field { get; init; }
    public required string Code { get; init; }
    public IReadOnlyDictionary<string, object?>? Params { get; init; }
}

public sealed class ApiGlobalErrorInput
{
    public required string Code { get; init; }
    public IReadOnlyDictionary<string, object?>? Params { get; init; }
}
