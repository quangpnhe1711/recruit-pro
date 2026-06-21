using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Infrastructure.Service;

public class AiResumeParserProvider : IResumeParsingAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiResumeParserProvider> _logger;
    private readonly AiProviderSettings _settings;

    /// <summary>
    /// Initializes a new instance of the AiResumeParserProvider class.
    /// </summary>
    /// <param name="httpClient">The <paramref name="httpClient"/> value.</param>
    /// <param name="options">The <paramref name="options"/> value.</param>
    /// <param name="logger">The <paramref name="logger"/> value.</param>
    public AiResumeParserProvider(
        HttpClient httpClient,
        IOptions<AiProviderSettings> options,
        ILogger<AiResumeParserProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = options.Value;
    }

    /// <summary>
    /// Attempts to parse resume.
    /// </summary>
    /// <param name="extractedText">The <paramref name="extractedText"/> value.</param>
    /// <param name="availableSkills">The <paramref name="availableSkills"/> value.</param>
    /// <param name="cancellationToken">The <paramref name="cancellationToken"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
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
                Provider = "AiCompatible",
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

        using HttpRequestMessage request = AiCompatibleApiHelper.BuildRequest(_settings, requestBody, JsonOptions);

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string? traceId = response.Headers.TryGetValues("x-request-id", out IEnumerable<string>? requestIds)
                    ? requestIds.FirstOrDefault()
                    : response.Headers.TryGetValues("request-id", out IEnumerable<string>? altRequestIds)
                        ? altRequestIds.FirstOrDefault()
                        : null;

                _logger.LogWarning(
                    "AI resume parser request failed. Provider={Provider}, Model={Model}, StatusCode={StatusCode}, TraceId={TraceId}, Response={Response}",
                    "AiCompatible",
                    _settings.Model,
                    (int)response.StatusCode,
                    traceId,
                    raw);
                return new ResumeParsingAiResult
                {
                    UsedAi = false,
                    Provider = "AiCompatible",
                    ModelName = _settings.Model,
                    FailureReason = $"AI provider returned HTTP {(int)response.StatusCode}.",
                    HttpStatusCode = (int)response.StatusCode,
                    RawProviderResponse = raw,
                    TraceId = traceId,
                    IsRetryable = IsRetryableStatusCode((int)response.StatusCode)
                };
            }

            string? outputText = AiCompatibleApiHelper.ExtractOutputText(raw);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                return new ResumeParsingAiResult
                {
                    UsedAi = false,
                    Provider = "AiCompatible",
                    ModelName = _settings.Model,
                    FailureReason = "AI provider returned no output text."
                };
            }

            string normalizedOutput = AiCompatibleApiHelper.NormalizeJsonPayload(outputText);
            CandidateResumeAiParseDto? parsed = JsonSerializer.Deserialize<CandidateResumeAiParseDto>(normalizedOutput, JsonOptions);
            if (parsed == null)
            {
                return new ResumeParsingAiResult
                {
                    UsedAi = false,
                    Provider = "AiCompatible",
                    ModelName = _settings.Model,
                    FailureReason = "AI provider returned output that could not be deserialized."
                };
            }

            return new ResumeParsingAiResult
            {
                UsedAi = true,
                Provider = "AiCompatible",
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
                Provider = "AiCompatible",
                ModelName = _settings.Model,
                FailureReason = "AI parsing timed out before a complete response was returned.",
                IsRetryable = true
            };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "AI resume parser failed.");
            return new ResumeParsingAiResult
            {
                UsedAi = false,
                Provider = "AiCompatible",
                ModelName = _settings.Model,
                FailureReason = exception.Message
            };
        }
    }

    /// <summary>
    /// Builds prompt.
    /// </summary>
    /// <param name="extractedText">The <paramref name="extractedText"/> value.</param>
    /// <param name="skillNames">The <paramref name="skillNames"/> value.</param>
    /// <returns>The resulting string value.</returns>
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
              "summary": "string or null",
              "email": "string or null",
              "phone": "string or null",
              "location": "string or null",
              "github": "string or null",
              "linkedin": "string or null",
              "portfolio": "string or null",
              "website": "string or null"
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
          "awards": [
            {
              "name": "string",
              "issuer": "string or null",
              "year": 2024,
              "description": "string or null"
            }
          ],
          "activities": [
            {
              "organization": "string",
              "role": "string or null",
              "description": "string or null",
              "startYear": 2023,
              "endYear": 2024
            }
          ],
          "keywords": ["backend developer", "asp.net core"],
          "parserWarnings": ["short parser note"]
        }

        Master skill list:
        {{JsonSerializer.Serialize(skillNames, JsonOptions)}}

        Resume text:
        {{boundedText}}
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

    /// <summary>
    /// Builds endpoint.
    /// </summary>
    /// <returns>The resulting string value.</returns>
    private static bool IsRetryableStatusCode(int statusCode)
    {
        return statusCode is 429 or 500 or 502 or 503 or 504;
    }
}
