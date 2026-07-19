using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Common;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Infrastructure.Service;

/// <summary>
/// v5.1 telemetry decorator for <see cref="IAiCopilotProvider"/>. Times each call, records a telemetry
/// row (feature, provider, model, tokens, latency, success/fallback/schema, risk flags), and returns the
/// inner result unchanged. Telemetry is best-effort and isolated in try/catch, so it can never alter the
/// provider's null-means-fallback contract or throw into the caller. Pure disabled/unconfigured
/// short-circuits are NOT recorded (nothing actually ran).
/// </summary>
public class TelemetryAiCopilotProvider : IAiCopilotProvider
{
    private readonly IAiCopilotProvider _inner;
    private readonly IAiTelemetryService _telemetry;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AiProviderSettings _settings;
    private readonly ILogger<TelemetryAiCopilotProvider> _logger;

    public TelemetryAiCopilotProvider(
        IAiCopilotProvider inner,
        IAiTelemetryService telemetry,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AiProviderSettings> options,
        ILogger<TelemetryAiCopilotProvider> logger)
    {
        _inner = inner;
        _telemetry = telemetry;
        _httpContextAccessor = httpContextAccessor;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<AiStructuredJsonResult> TryCreateStructuredJsonAsync(
        string actionType, string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        bool configured = TelemetryProviderContext.IsConfigured(_settings);
        Stopwatch stopwatch = Stopwatch.StartNew();
        AiStructuredJsonResult result = await _inner.TryCreateStructuredJsonAsync(actionType, systemPrompt, userPrompt, cancellationToken);
        stopwatch.Stop();

        if (configured)
        {
            string feature = string.IsNullOrWhiteSpace(actionType) ? AiFeatureKeys.InterviewQuestions : actionType;
            List<string> riskFlags = new();
            if (!result.Succeeded)
            {
                riskFlags.Add(AiRiskFlags.FallbackUsed);
                riskFlags.Add(AiRiskFlags.ProviderError);
            }

            await SafeRecordAsync(new AiTelemetryRecord
            {
                Feature = feature,
                ProviderName = string.IsNullOrWhiteSpace(result.ProviderName) ? TelemetryProviderContext.ResolveProviderHost(_settings) : result.ProviderName,
                ModelName = string.IsNullOrWhiteSpace(result.ModelName) ? _settings.Model : result.ModelName,
                PromptTokens = result.PromptTokens,
                CompletionTokens = result.CompletionTokens,
                TotalTokens = result.TotalTokens,
                LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                Success = result.Succeeded,
                FallbackUsed = !result.Succeeded,
                SchemaValid = result.Succeeded,
                ErrorCode = result.Succeeded ? null : "provider_failure",
                ErrorMessage = result.FailureReason,
                CorrelationId = TelemetryProviderContext.GetCorrelationId(_httpContextAccessor),
                UserId = TelemetryProviderContext.GetUserId(_httpContextAccessor),
                RiskFlags = riskFlags.Count == 0 ? null : riskFlags,
                Metadata = new Dictionary<string, object?> { ["actionType"] = actionType },
            });
        }

        return result;
    }

    public async Task<CopilotPromptResponseDto?> TryCreateRankingAsync(
        CopilotCandidatePoolDto pool, CopilotNormalizedRulesDto rules,
        IReadOnlyList<CopilotRankingResultDto> deterministicResults, string userPrompt,
        Guid conversationId, CancellationToken cancellationToken = default)
    {
        bool configured = TelemetryProviderContext.IsConfigured(_settings);
        Stopwatch stopwatch = Stopwatch.StartNew();
        CopilotPromptResponseDto? result = await _inner.TryCreateRankingAsync(pool, rules, deterministicResults, userPrompt, conversationId, cancellationToken);
        stopwatch.Stop();

        if (configured)
        {
            bool success = result is not null;
            await SafeRecordAsync(new AiTelemetryRecord
            {
                Feature = AiFeatureKeys.Ranking,
                ProviderName = TelemetryProviderContext.ResolveProviderHost(_settings),
                ModelName = _settings.Model,
                LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                Success = success,
                FallbackUsed = !success,
                SchemaValid = success,
                ErrorCode = success ? null : "provider_failure",
                ErrorMessage = success ? null : "AI ranking enrichment unavailable; deterministic ranking used.",
                CorrelationId = TelemetryProviderContext.GetCorrelationId(_httpContextAccessor),
                UserId = TelemetryProviderContext.GetUserId(_httpContextAccessor),
                RiskFlags = success ? null : new[] { AiRiskFlags.FallbackUsed },
                Metadata = new Dictionary<string, object?>
                {
                    ["conversationId"] = conversationId,
                    ["candidateCount"] = pool.Candidates?.Count ?? 0,
                },
            });
        }

        return result;
    }

    public async Task<string?> TryCreateChatReplyAsync(
        CopilotCandidatePoolDto pool, string userPrompt, IReadOnlyList<CopilotMessage> history, Guid conversationId, CancellationToken cancellationToken = default)
    {
        bool configured = TelemetryProviderContext.IsConfigured(_settings);
        Stopwatch stopwatch = Stopwatch.StartNew();
        string? result = await _inner.TryCreateChatReplyAsync(pool, userPrompt, history, conversationId, cancellationToken);
        stopwatch.Stop();

        if (configured)
        {
            bool success = !string.IsNullOrWhiteSpace(result) && !LooksLikeError(result);
            await SafeRecordAsync(new AiTelemetryRecord
            {
                Feature = AiFeatureKeys.CopilotChat,
                ProviderName = TelemetryProviderContext.ResolveProviderHost(_settings),
                ModelName = _settings.Model,
                LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                Success = success,
                FallbackUsed = !success,
                ErrorCode = success ? null : "provider_failure",
                ErrorMessage = success ? null : result,
                CorrelationId = TelemetryProviderContext.GetCorrelationId(_httpContextAccessor),
                UserId = TelemetryProviderContext.GetUserId(_httpContextAccessor),
                RiskFlags = success ? null : new[] { AiRiskFlags.ProviderError },
                Metadata = new Dictionary<string, object?> { ["conversationId"] = conversationId },
            });
        }

        return result;
    }

    private static bool LooksLikeError(string reply)
        => reply.StartsWith("AI provider error", StringComparison.OrdinalIgnoreCase)
        || reply.StartsWith("AI provider request failed", StringComparison.OrdinalIgnoreCase)
        || reply.StartsWith("AI provider returned no", StringComparison.OrdinalIgnoreCase);

    private async Task SafeRecordAsync(AiTelemetryRecord record)
    {
        try
        {
            await _telemetry.RecordAsync(record);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "AI copilot telemetry record failed for feature {Feature}.", record.Feature);
        }
    }
}
