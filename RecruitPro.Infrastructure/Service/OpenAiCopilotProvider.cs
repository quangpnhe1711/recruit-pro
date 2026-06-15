using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.Infrastructure.Service;

public class OpenAiCopilotProvider : IAiCopilotProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string OpenAiResponsesSuffix = "/responses";
    private const string OpenAiChatCompletionsSuffix = "/chat/completions";
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiCopilotProvider> _logger;
    private readonly OpenAiSettings _settings;

    public OpenAiCopilotProvider(
        HttpClient httpClient,
        IOptions<OpenAiSettings> options,
        ILogger<OpenAiCopilotProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = options.Value;
    }

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
                "OpenAI Copilot skipped. Enabled: {Enabled}. ApiKeyPresent: {ApiKeyPresent}.",
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

        using HttpRequestMessage request = BuildRequest(requestBody);

        try
        {
            _logger.LogInformation(
                "OpenAI Copilot request started. Model: {Model}. CandidatesSent: {CandidateCount}. ConversationId: {ConversationId}.",
                _settings.Model,
                payload.candidates.Count(),
                conversationId);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI Copilot request failed with status {StatusCode}: {Response}", response.StatusCode, raw);
                return null;
            }

            string? outputText = ExtractOutputText(raw);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                return null;
            }

            string normalizedOutput = NormalizeJsonPayload(outputText);
            CopilotPromptResponseDto? aiResponse = JsonSerializer.Deserialize<CopilotPromptResponseDto>(normalizedOutput, JsonOptions);
            if (aiResponse is null)
            {
                _logger.LogWarning("OpenAI Copilot response could not be deserialized for conversation {ConversationId}.", conversationId);
                return null;
            }

            aiResponse.ConversationId = conversationId;
            _logger.LogInformation(
                "OpenAI Copilot request completed. Model: {Model}. ConversationId: {ConversationId}. ResultCount: {ResultCount}.",
                _settings.Model,
                conversationId,
                aiResponse.Results.Count);
            return aiResponse;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI Copilot provider failed. Falling back to deterministic ranking.");
            return null;
        }
    }

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

        using HttpRequestMessage request = BuildRequest(requestBody);

        try
        {
            _logger.LogInformation(
                "OpenAI Copilot chat reply request started. Model: {Model}. CandidatesSent: {CandidateCount}. ConversationId: {ConversationId}.",
                _settings.Model,
                payload.candidates.Count(),
                conversationId);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI Copilot chat reply failed with status {StatusCode}: {Response}", response.StatusCode, raw);
                return BuildChatErrorMessage($"AI provider request failed with status {(int)response.StatusCode}.", raw);
            }

            string? outputText = ExtractOutputText(raw);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                _logger.LogWarning(
                    "OpenAI Copilot chat reply returned no output text. ConversationId: {ConversationId}. RawResponse: {Response}",
                    conversationId,
                    raw);
                return BuildChatErrorMessage("AI provider returned no message content.", raw);
            }

            _logger.LogInformation(
                "OpenAI Copilot chat reply completed. Model: {Model}. ConversationId: {ConversationId}.",
                _settings.Model,
                conversationId);

            return outputText;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI Copilot chat reply failed.");
            return $"AI provider error: {ex.Message}";
        }
    }

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

    private HttpRequestMessage BuildRequest(object requestBody)
    {
        string endpoint = BuildEndpoint();
        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        return request;
    }

    private object BuildRequestBody(string systemPrompt, string userPrompt, bool requireJson)
    {
        if (UsesChatCompletions())
        {
            return new
            {
                model = _settings.Model,
                temperature = requireJson ? 0.1 : 0.4,
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

    private string BuildEndpoint()
    {
        string baseUrl = _settings.BaseUrl.TrimEnd('/');
        return UsesChatCompletions()
            ? $"{baseUrl}{OpenAiChatCompletionsSuffix}"
            : $"{baseUrl}{OpenAiResponsesSuffix}";
    }

    private bool UsesChatCompletions()
    {
        return _settings.BaseUrl.Contains("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase)
            || _settings.BaseUrl.EndsWith("/openai", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractOutputText(string rawResponse)
    {
        using JsonDocument document = JsonDocument.Parse(rawResponse);
        if (document.RootElement.TryGetProperty("output_text", out JsonElement outputText))
        {
            return outputText.GetString();
        }

        if (document.RootElement.TryGetProperty("choices", out JsonElement choices) && choices.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement choice in choices.EnumerateArray())
            {
                if (!choice.TryGetProperty("message", out JsonElement message))
                {
                    continue;
                }

                if (!message.TryGetProperty("content", out JsonElement content))
                {
                    continue;
                }

                if (content.ValueKind == JsonValueKind.String)
                {
                    return content.GetString();
                }

                if (content.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement contentPart in content.EnumerateArray())
                {
                    if (contentPart.TryGetProperty("text", out JsonElement text))
                    {
                        return text.GetString();
                    }
                }
            }
        }

        if (!document.RootElement.TryGetProperty("output", out JsonElement output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement outputItem in output.EnumerateArray())
        {
            if (!outputItem.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out JsonElement text))
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }

    private static string NormalizeJsonPayload(string payload)
    {
        string trimmed = payload.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        int firstLineEnd = trimmed.IndexOf('\n');
        if (firstLineEnd < 0)
        {
            return trimmed.Trim('`').Trim();
        }

        string withoutHeader = trimmed[(firstLineEnd + 1)..];
        int closingFenceIndex = withoutHeader.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFenceIndex >= 0)
        {
            withoutHeader = withoutHeader[..closingFenceIndex];
        }

        return withoutHeader.Trim();
    }

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
