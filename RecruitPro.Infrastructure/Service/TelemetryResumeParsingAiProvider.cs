using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Common;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Infrastructure.Service;

/// <summary>
/// v5.1 telemetry decorator for <see cref="IResumeParsingAiProvider"/>. Records feature="resume_parsing"
/// telemetry. UsedAi=false means the deterministic preview path was taken (fallback). A schema-invalid /
/// undeserializable response is flagged. Best-effort; never alters the inner result or throws.
/// </summary>
public class TelemetryResumeParsingAiProvider : IResumeParsingAiProvider
{
    private readonly IResumeParsingAiProvider _inner;
    private readonly IAiTelemetryService _telemetry;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AiProviderSettings _settings;
    private readonly ILogger<TelemetryResumeParsingAiProvider> _logger;

    public TelemetryResumeParsingAiProvider(
        IResumeParsingAiProvider inner,
        IAiTelemetryService telemetry,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AiProviderSettings> options,
        ILogger<TelemetryResumeParsingAiProvider> logger)
    {
        _inner = inner;
        _telemetry = telemetry;
        _httpContextAccessor = httpContextAccessor;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<ResumeParsingAiResult> TryParseResumeAsync(
        string extractedText, IReadOnlyList<Skill> availableSkills, CancellationToken cancellationToken = default)
    {
        bool configured = TelemetryProviderContext.IsConfigured(_settings);
        Stopwatch stopwatch = Stopwatch.StartNew();
        ResumeParsingAiResult result = await _inner.TryParseResumeAsync(extractedText, availableSkills, cancellationToken);
        stopwatch.Stop();

        if (configured)
        {
            try
            {
                bool schemaInvalid = !result.UsedAi
                    && result.FailureReason is not null
                    && result.FailureReason.Contains("deserialize", StringComparison.OrdinalIgnoreCase);
                List<string> riskFlags = new();
                if (!result.UsedAi)
                {
                    riskFlags.Add(AiRiskFlags.FallbackUsed);
                }
                if (schemaInvalid)
                {
                    riskFlags.Add(AiRiskFlags.SchemaInvalid);
                }

                await _telemetry.RecordAsync(new AiTelemetryRecord
                {
                    Feature = AiFeatureKeys.ResumeParsing,
                    ProviderName = TelemetryProviderContext.ResolveProviderHost(_settings),
                    ModelName = string.IsNullOrWhiteSpace(result.ModelName) ? _settings.Model : result.ModelName,
                    PromptTokens = result.PromptTokens,
                    CompletionTokens = result.CompletionTokens,
                    TotalTokens = result.TotalTokens,
                    LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                    Success = result.UsedAi,
                    FallbackUsed = !result.UsedAi,
                    SchemaValid = result.UsedAi ? true : (schemaInvalid ? false : (bool?)null),
                    ErrorCode = result.UsedAi ? null : (result.HttpStatusCode.HasValue ? $"http_{result.HttpStatusCode}" : "provider_failure"),
                    ErrorMessage = result.FailureReason,
                    CorrelationId = TelemetryProviderContext.GetCorrelationId(_httpContextAccessor),
                    UserId = TelemetryProviderContext.GetUserId(_httpContextAccessor),
                    RiskFlags = riskFlags.Count == 0 ? null : riskFlags,
                });
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Resume parsing telemetry record failed.");
            }
        }

        return result;
    }
}
