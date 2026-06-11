using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.Infrastructure.Service;

public class OpenAiCopilotProvider : IAiCopilotProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
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

        var requestBody = new
        {
            model = _settings.Model,
            input = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are an ATS recruitment copilot. Return only valid JSON matching the requested schema. Keep auto rejected candidates at the end."
                },
                new
                {
                    role = "user",
                    content = BuildPrompt(payload)
                }
            }
        };

        using HttpRequestMessage request = new(HttpMethod.Post, $"{_settings.BaseUrl.TrimEnd('/')}/responses")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        try
        {
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

            CopilotPromptResponseDto? aiResponse = JsonSerializer.Deserialize<CopilotPromptResponseDto>(outputText, JsonOptions);
            if (aiResponse is null)
            {
                return null;
            }

            aiResponse.ConversationId = conversationId;
            return aiResponse;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI Copilot provider failed. Falling back to deterministic ranking.");
            return null;
        }
    }

    private static string BuildPrompt(object payload)
    {
        return $$"""
        Rank candidates for this ATS job.
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
              "summary": "short explanation"
            }
          ]
        }

        Data:
        {{JsonSerializer.Serialize(payload, JsonOptions)}}
        """;
    }

    private static string? ExtractOutputText(string rawResponse)
    {
        using JsonDocument document = JsonDocument.Parse(rawResponse);
        if (document.RootElement.TryGetProperty("output_text", out JsonElement outputText))
        {
            return outputText.GetString();
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
}
