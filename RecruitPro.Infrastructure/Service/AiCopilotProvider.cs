using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.Infrastructure.Service;

public class AiCopilotProvider : IAiCopilotProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiCopilotProvider> _logger;
    private readonly AiProviderSettings _settings;

    /// <summary>
    /// Initializes a new instance of the AiCopilotProvider class.
    /// </summary>
    /// <param name="httpClient">The <paramref name="httpClient"/> value.</param>
    /// <param name="options">The <paramref name="options"/> value.</param>
    /// <param name="logger">The <paramref name="logger"/> value.</param>
    public AiCopilotProvider(
        HttpClient httpClient,
        IOptions<AiProviderSettings> options,
        ILogger<AiCopilotProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = options.Value;
    }

    public async Task<AiStructuredJsonResult> TryCreateStructuredJsonAsync(
        string actionType,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        string providerName = ResolveProviderName();
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogInformation(
                "Structured AI copilot skipped. Action: {ActionType}. Enabled: {Enabled}. ApiKeyPresent: {ApiKeyPresent}.",
                actionType,
                _settings.Enabled,
                !string.IsNullOrWhiteSpace(_settings.ApiKey));
            return AiStructuredJsonResult.Failure("AI provider disabled or missing API configuration.", providerName, _settings.Model);
        }

        object requestBody = BuildRequestBody(
            systemPrompt: systemPrompt,
            userPrompt: userPrompt,
            requireJson: true);

        using HttpRequestMessage request = AiCompatibleApiHelper.BuildRequest(_settings, requestBody, JsonOptions);

        try
        {
            _logger.LogInformation(
                "Structured AI copilot request started. Action: {ActionType}. Model: {Model}.",
                actionType,
                _settings.Model);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Structured AI copilot request failed. Action: {ActionType}. Status: {StatusCode}. Response: {Response}",
                    actionType,
                    response.StatusCode,
                    raw);
                return AiStructuredJsonResult.Failure($"AI provider request failed with status {(int)response.StatusCode}.", providerName, _settings.Model);
            }

            string? outputText = AiCompatibleApiHelper.ExtractOutputText(raw);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                return AiStructuredJsonResult.Failure("AI provider returned no JSON content.", providerName, _settings.Model);
            }

            string normalizedOutput = AiCompatibleApiHelper.NormalizeJsonPayload(outputText);
            return AiStructuredJsonResult.Success(normalizedOutput, providerName, _settings.Model);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Structured AI copilot provider failed. Action: {ActionType}.", actionType);
            return AiStructuredJsonResult.Failure(ex.Message, providerName, _settings.Model);
        }
    }

    /// <summary>
    /// Attempts to create ranking.
    /// </summary>
    /// <param name="pool">The <paramref name="pool"/> value.</param>
    /// <param name="rules">The <paramref name="rules"/> value.</param>
    /// <param name="deterministicResults">The <paramref name="deterministicResults"/> value.</param>
    /// <param name="userPrompt">The <paramref name="userPrompt"/> value.</param>
    /// <param name="conversationId">The <paramref name="conversationId"/> value.</param>
    /// <param name="cancellationToken">The <paramref name="cancellationToken"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<CopilotPromptResponseDto?> TryCreateRankingAsync(
        CopilotCandidatePoolDto pool,
        CopilotNormalizedRulesDto rules,
        IReadOnlyList<CopilotRankingResultDto> deterministicResults,
        string userPrompt,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogInformation(
                "AI copilot skipped. Enabled: {Enabled}. ApiKeyPresent: {ApiKeyPresent}.",
                _settings.Enabled,
                !string.IsNullOrWhiteSpace(_settings.ApiKey));
            return null;
        }

        int candidateLimit = _settings.MaxCandidatesForAi > 0 ? _settings.MaxCandidatesForAi : 50;
        var payload = new
        {
            job = pool.Job,
            userPrompt,
            normalizedRules = rules,
            candidates = pool.Candidates.Take(candidateLimit),
            deterministicRanking = deterministicResults.Take(candidateLimit)
        };

        object requestBody = BuildRequestBody(
            systemPrompt: "You are an ATS recruitment copilot. Return only valid JSON matching the requested schema. Keep auto rejected candidates at the end. Every candidate result must include a concrete summary/match reason grounded in the CV, skills, experience, and active criteria.",
            userPrompt: BuildPrompt(payload),
            requireJson: true);

        using HttpRequestMessage request = AiCompatibleApiHelper.BuildRequest(_settings, requestBody, JsonOptions);

        try
        {
            _logger.LogInformation(
                "AI copilot request started. Model: {Model}. CandidatesSent: {CandidateCount}. ConversationId: {ConversationId}.",
                _settings.Model,
                payload.candidates.Count(),
                conversationId);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI copilot request failed with status {StatusCode}: {Response}", response.StatusCode, raw);
                return null;
            }

            string? outputText = AiCompatibleApiHelper.ExtractOutputText(raw);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                return null;
            }

            string normalizedOutput = AiCompatibleApiHelper.NormalizeJsonPayload(outputText);
            CopilotPromptResponseDto? aiResponse = JsonSerializer.Deserialize<CopilotPromptResponseDto>(normalizedOutput, JsonOptions);
            if (aiResponse is null)
            {
                _logger.LogWarning("AI copilot response could not be deserialized for conversation {ConversationId}.", conversationId);
                return null;
            }

            aiResponse.ConversationId = conversationId;
            _logger.LogInformation(
                "AI copilot request completed. Model: {Model}. ConversationId: {ConversationId}. ResultCount: {ResultCount}.",
                _settings.Model,
                conversationId,
                aiResponse.Results.Count);
            return aiResponse;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI copilot provider failed. Falling back to deterministic ranking.");
            return null;
        }
    }

    /// <summary>
    /// Attempts to create chat reply.
    /// </summary>
    /// <param name="pool">The <paramref name="pool"/> value.</param>
    /// <param name="userPrompt">The <paramref name="userPrompt"/> value.</param>
    /// <param name="conversationId">The <paramref name="conversationId"/> value.</param>
    /// <param name="cancellationToken">The <paramref name="cancellationToken"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<string?> TryCreateChatReplyAsync(
        CopilotCandidatePoolDto pool,
        string userPrompt,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return "AI copilot is disabled or missing API configuration.";
        }

        int candidateLimit = Math.Min(_settings.MaxCandidatesForAi > 0 ? _settings.MaxCandidatesForAi : 20, 8);
        var payload = new
        {
            job = pool.Job,
            userPrompt,
            candidates = pool.Candidates.Take(candidateLimit).Select(candidate => new
            {
                candidate.FullName,
                candidate.ExperienceYears,
                candidate.Education,
                candidate.CvSummary
            })
        };

        object requestBody = BuildRequestBody(
            systemPrompt: "You are an ATS recruitment copilot. Answer the recruiter naturally in the same language as the user's question when possible. Base every answer on the provided job and CV evidence. Do not rank candidates unless the user explicitly asks for ranking, scoring, screening, shortlist, top candidates, or evaluation.",
            userPrompt: $$"""
                Answer the recruiter's question using the job details and CV evidence below.
                Keep it concise and useful.

                Data:
                {{JsonSerializer.Serialize(payload, JsonOptions)}}
                """,
            requireJson: false);

        using HttpRequestMessage request = AiCompatibleApiHelper.BuildRequest(_settings, requestBody, JsonOptions);

        try
        {
            _logger.LogInformation(
                "AI copilot chat reply request started. Model: {Model}. CandidatesSent: {CandidateCount}. ConversationId: {ConversationId}.",
                _settings.Model,
                payload.candidates.Count(),
                conversationId);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI copilot chat reply failed with status {StatusCode}: {Response}", response.StatusCode, raw);
                return BuildChatErrorMessage($"AI provider request failed with status {(int)response.StatusCode}.", raw);
            }

            string? outputText = AiCompatibleApiHelper.ExtractOutputText(raw);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                _logger.LogWarning(
                    "AI copilot chat reply returned no output text. ConversationId: {ConversationId}. RawResponse: {Response}",
                    conversationId,
                    raw);
                return BuildChatErrorMessage("AI provider returned no message content.", raw);
            }

            _logger.LogInformation(
                "AI copilot chat reply completed. Model: {Model}. ConversationId: {ConversationId}.",
                _settings.Model,
                conversationId);

            return outputText;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI copilot chat reply failed.");
            return $"AI provider error: {ex.Message}";
        }
    }

    /// <summary>
    /// Builds prompt.
    /// </summary>
    /// <param name="payload">The <paramref name="payload"/> value.</param>
    /// <returns>The resulting string value.</returns>
    private static string BuildPrompt(object payload)
    {
        return $$"""
        Rank candidates for this ATS job.
        Important rules:
        - Return exactly one result per candidateUserId.
        - Always fill summary with a concrete match reason.
        - Summary must explain why the candidate is ranked there, mentioning matched strengths, missing requirements, and any penalty-only criteria impact when relevant.
        - If the candidate is auto rejected, summary must still explain the rejection.
        Required output JSON shape:
        {
          "conversationId": "00000000-0000-0000-0000-000000000000",
          "rankingSessionId": "00000000-0000-0000-0000-000000000000",
          "normalizedRules": { "requiredSkills": [], "preferredSkills": [], "minExperienceYears": null, "autoRejectRules": [], "minTotalScore": null },
          "results": [
            {
              "candidateUserId": "uuid",
              "applicationId": "uuid",
              "fullName": "name",
              "rankPosition": 1,
              "totalScore": 90,
              "skillScore": 40,
              "experienceScore": 30,
              "educationScore": 15,
              "projectScore": 5,
              "recommendation": "Interview",
              "isAutoRejected": false,
              "rejectReason": null,
              "strengths": [],
              "weaknesses": [],
              "summary": "AI-generated match reason tied to the candidate profile and hiring criteria",
              "isAiGenerated": true
            }
          ]
        }

        Data:
        {{JsonSerializer.Serialize(payload, JsonOptions)}}
        """;
    }

    /// <summary>
    /// Builds request body.
    /// </summary>
    /// <param name="systemPrompt">The <paramref name="systemPrompt"/> value.</param>
    /// <param name="userPrompt">The <paramref name="userPrompt"/> value.</param>
    /// <param name="requireJson">The <paramref name="requireJson"/> value.</param>
    /// <returns>The operation result.</returns>
    private object BuildRequestBody(string systemPrompt, string userPrompt, bool requireJson)
    {
        if (AiCompatibleApiHelper.UsesChatCompletions(_settings))
        {
            return new
            {
                model = _settings.Model,
                temperature = requireJson ? 0.1 : 0.4,
                reasoning_effort = requireJson ? "low" : null,
                max_completion_tokens = requireJson ? 3200 : 1200,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = systemPrompt
                    },
                    new
                    {
                        role = "user",
                        content = userPrompt
                    }
                }
            };
        }

        return new
        {
            model = _settings.Model,
            max_output_tokens = requireJson ? 3200 : 1200,
            input = new object[]
            {
                new
                {
                    role = "system",
                    content = systemPrompt
                },
                new
                {
                    role = "user",
                    content = userPrompt
                }
            }
        };
    }

    private string ResolveProviderName()
    {
        if (Uri.TryCreate(_settings.BaseUrl, UriKind.Absolute, out Uri? uri))
        {
            return uri.Host;
        }

        return "configured-ai-provider";
    }

    /// <summary>
    /// Builds chat error message.
    /// </summary>
    /// <param name="summary">The <paramref name="summary"/> value.</param>
    /// <param name="rawResponse">The <paramref name="rawResponse"/> value.</param>
    /// <returns>The resulting string value.</returns>
    private static string BuildChatErrorMessage(string summary, string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return summary;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(rawResponse);
            if (document.RootElement.TryGetProperty("error", out JsonElement error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return $"{summary} {error.GetString()}".Trim();
                }

                if (error.ValueKind == JsonValueKind.Object)
                {
                    if (error.TryGetProperty("message", out JsonElement message))
                    {
                        return $"{summary} {message.GetString()}".Trim();
                    }

                    if (error.TryGetProperty("status", out JsonElement status))
                    {
                        return $"{summary} {status.GetString()}".Trim();
                    }
                }
            }
        }
        catch
        {
        }

        string compact = rawResponse.Trim();
        if (compact.Length > 300)
        {
            compact = compact[..300] + "...";
        }

        return $"{summary} {compact}".Trim();
    }
}
