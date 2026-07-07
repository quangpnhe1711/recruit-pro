namespace RecruitPro.Application.Common;

/// <summary>
/// Central resolver: error <c>code</c> → human-readable <c>message</c>. This is the ONLY place the API
/// turns a code into a message — controllers/services must never hardcode error copy. The message is a
/// debug-friendly payload for F12/Postman/logs/non-web clients; the web frontend ignores it and maps the
/// <c>code</c> (+ params) to its own i18n dictionary (see ERROR-CONTRACT.md).
/// </summary>
public interface IErrorMessageProvider
{
    /// <summary>Resolve <paramref name="code"/> to a message, interpolating <c>{param}</c> placeholders.</summary>
    string GetMessage(string code, IReadOnlyDictionary<string, object?>? parameters = null, string? locale = null);
}
