using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Common;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Constants;

namespace RecruitPro.Infrastructure.Service;

/// <summary>
/// v5.1 telemetry decorator for <see cref="IEmbeddingProvider"/>. Records feature="embedding" telemetry
/// (provider/model/tokens/latency/success). Runs from background workers too, where there is no
/// HttpContext, so user/correlation are null. Best-effort; never alters the inner result or throws.
/// </summary>
public class TelemetryEmbeddingProvider : IEmbeddingProvider
{
    private readonly IEmbeddingProvider _inner;
    private readonly IAiTelemetryService _telemetry;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AiProviderSettings _settings;
    private readonly ILogger<TelemetryEmbeddingProvider> _logger;

    public TelemetryEmbeddingProvider(
        IEmbeddingProvider inner,
        IAiTelemetryService telemetry,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AiProviderSettings> options,
        ILogger<TelemetryEmbeddingProvider> logger)
    {
        _inner = inner;
        _telemetry = telemetry;
        _httpContextAccessor = httpContextAccessor;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<EmbeddingGenerationResult> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        bool configured = TelemetryProviderContext.IsConfigured(_settings);
        Stopwatch stopwatch = Stopwatch.StartNew();
        EmbeddingGenerationResult result = await _inner.GenerateEmbeddingAsync(text, cancellationToken);
        stopwatch.Stop();

        if (configured)
        {
            try
            {
                await _telemetry.RecordAsync(new AiTelemetryRecord
                {
                    Feature = AiFeatureKeys.Embedding,
                    ProviderName = TelemetryProviderContext.ResolveProviderHost(_settings),
                    ModelName = string.IsNullOrWhiteSpace(result.ModelName) ? _settings.EmbeddingModel : result.ModelName,
                    PromptTokens = result.PromptTokens,
                    CompletionTokens = result.CompletionTokens,
                    TotalTokens = result.TotalTokens,
                    LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                    Success = result.Succeeded,
                    FallbackUsed = !result.Succeeded,
                    ErrorCode = result.Succeeded ? null : (result.HttpStatusCode.HasValue ? $"http_{result.HttpStatusCode}" : "provider_failure"),
                    ErrorMessage = result.FailureReason,
                    CorrelationId = TelemetryProviderContext.GetCorrelationId(_httpContextAccessor),
                    UserId = TelemetryProviderContext.GetUserId(_httpContextAccessor),
                    RiskFlags = result.Succeeded ? null : new[] { AiRiskFlags.ProviderError },
                });
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Embedding telemetry record failed.");
            }
        }

        return result;
    }
}
