using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Infrastructure.Service;

public class OpenAiResumeParserProvider : IResumeParsingAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string OpenAiResponsesSuffix = "/responses";
    private const string OpenAiChatCompletionsSuffix = "/chat/completions";
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiResumeParserProvider> _logger;
    private readonly OpenAiSettings _settings;

    public OpenAiResumeParserProvider(
        HttpClient httpClient,
        IOptions<OpenAiSettings> options,
        ILogger<OpenAiResumeParserProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = options.Value;
    }

    public async Task<ResumeParsingAiResult> TryParseResumeAsync(
        string extractedText,
        IReadOnlyList<Skill> availableSkills,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(extractedText))
        {
            return new ResumeParsingAiResult
            {
                UsedAi = false,
                ModelName = _settings.Model,
                FailureReason = !_settings.Enabled
                    ? "AI parsing is disabled in configuration."
                    : string.IsNullOrWhiteSpace(_settings.ApiKey)
                        ? "AI parsing is missing API key configuration."
                        : "Resume text is empty after extraction."
            };
        }

        string[] skillNames = availableSkills
            .Select(skill => skill.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .Take(250)
            .ToArray();

        object requestBody = BuildRequestBody(
            systemPrompt: """
                You are an ATS resume parsing engine.
                Extract structured candidate data from the provided resume text.
                Return only valid JSON matching the requested schema.
                Do not hallucinate facts not supported by the resume.
                Use null or empty arrays when information is missing.
                Skills must prefer names that exist in the provided master skill list whenever possible.
                Years of experience for skills should be conservative and only included when the resume strongly implies them.
                """,
            userPrompt: BuildPrompt(extractedText, skillNames),
            requireJson: true);

        using HttpRequestMessage request = BuildRequest(requestBody);

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI resume parser request failed with status {StatusCode}: {Response}", response.StatusCode, raw);
                return new ResumeParsingAiResult
                {
                    UsedAi = false,
                    ModelName = _settings.Model,
                    FailureReason = $"AI provider returned HTTP {(int)response.StatusCode}."
                };
            }

            string? outputText = ExtractOutputText(raw);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                return new ResumeParsingAiResult
                {
                    UsedAi = false,
                    ModelName = _settings.Model,
                    FailureReason = "AI provider returned no output text."
                };
            }

            string normalizedOutput = NormalizeJsonPayload(outputText);
            CandidateResumeAiParseDto? parsed = JsonSerializer.Deserialize<CandidateResumeAiParseDto>(normalizedOutput, JsonOptions);
            if (parsed == null)
            {
                return new ResumeParsingAiResult
                {
                    UsedAi = false,
                    ModelName = _settings.Model,
                    FailureReason = "AI provider returned output that could not be deserialized."
                };
            }

            return new ResumeParsingAiResult
            {
                UsedAi = true,
                ModelName = _settings.Model,
                Data = parsed
            };
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "AI resume parser timed out.");
            return new ResumeParsingAiResult
            {
                UsedAi = false,
                ModelName = _settings.Model,
                FailureReason = "AI parsing timed out before a complete response was returned."
            };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "AI resume parser failed.");
            return new ResumeParsingAiResult
            {
                UsedAi = false,
                ModelName = _settings.Model,
                FailureReason = exception.Message
            };
        }
    }

    private string BuildPrompt(string extractedText, string[] skillNames)
    {
        int maxResumeChars = _settings.MaxResumeParseChars > 0 ? _settings.MaxResumeParseChars : 12000;
        string boundedText = extractedText.Length > maxResumeChars ? extractedText[..maxResumeChars] : extractedText;
        return $$"""
        Parse the resume below into structured JSON.

        Required JSON shape:
        {
          "profile": {
            "name": "string or null",
            "headline": "string or null",
            "email": "string or null",
            "phone": "string or null",
            "location": "string or null",
            "bio": "string or null",
            "github": "string or null",
            "linkedin": "string or null"
          },
          "skills": [
            {
              "name": "skill from master list when possible",
              "yearsOfExperience": 0.5
            }
          ],
          "experienceEntries": [
            {
              "title": "string",
              "company": "string",
              "startMonth": 1,
              "startYear": 2022,
              "endMonth": 6,
              "endYear": 2024,
              "isCurrent": false,
              "bullets": ["string"]
            }
          ],
          "projects": [
            {
              "name": "string",
              "role": "string or null",
              "description": "string or null",
              "technologies": ["string"],
              "startMonth": 1,
              "startYear": 2023,
              "endMonth": 4,
              "endYear": 2024,
              "isCurrent": false
            }
          ],
          "educations": [
            {
              "school": "string",
              "degree": "string",
              "fieldOfStudy": "string or null",
              "startYear": 2020,
              "endYear": 2024,
              "description": "string or null"
            }
          ],
          "certifications": [
            {
              "name": "string",
              "issuer": "string or null",
              "issuedOn": "2024-01-15T00:00:00Z or null",
              "expiresOn": "2026-01-15T00:00:00Z or null",
              "credentialId": "string or null",
              "credentialUrl": "string or null"
            }
          ],
          "languages": [
            {
              "name": "string",
              "proficiency": "string"
            }
          ],
          "notes": ["short parser notes"]
        }

        Master skill list:
        {{JsonSerializer.Serialize(skillNames, JsonOptions)}}

        Resume text:
        {{boundedText}}
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
                temperature = 0.1,
                reasoning_effort = "low",
                max_completion_tokens = 2400,
                response_format = requireJson ? new { type = "json_object" } : null,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            };
        }

        return new
        {
            model = _settings.Model,
            max_output_tokens = 2400,
            text = requireJson ? new
            {
                format = new
                {
                    type = "json_object"
                }
            } : null,
            input = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
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
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLineBreak = trimmed.IndexOf('\n');
            if (firstLineBreak >= 0)
            {
                trimmed = trimmed[(firstLineBreak + 1)..];
            }

            int closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
            {
                trimmed = trimmed[..closingFence];
            }
        }

        return trimmed.Trim();
    }
}
