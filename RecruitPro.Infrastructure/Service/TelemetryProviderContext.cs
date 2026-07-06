using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RecruitPro.Application.Configurations;

namespace RecruitPro.Infrastructure.Service;

/// <summary>
/// v5.1 — shared helpers for the AI provider telemetry decorators: pulls the acting user id and a request
/// correlation id off the ambient HttpContext (null for background/worker calls), resolves the configured
/// provider host, and decides whether AI is actually configured. All null-safe; never throws.
/// </summary>
internal static class TelemetryProviderContext
{
    public static bool IsConfigured(AiProviderSettings settings)
        => settings.Enabled && !string.IsNullOrWhiteSpace(settings.ApiKey);

    public static Guid? GetUserId(IHttpContextAccessor accessor)
    {
        ClaimsPrincipal? user = accessor.HttpContext?.User;
        if (user is null)
        {
            return null;
        }

        string? raw = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(raw, out Guid id) ? id : null;
    }

    /// <summary>Request correlation id (ASP.NET TraceIdentifier), matching the error-response traceId.</summary>
    public static string? GetCorrelationId(IHttpContextAccessor accessor)
        => accessor.HttpContext?.TraceIdentifier;

    public static string ResolveProviderHost(AiProviderSettings settings)
        => Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out Uri? uri) ? uri.Host : "configured-ai-provider";
}
