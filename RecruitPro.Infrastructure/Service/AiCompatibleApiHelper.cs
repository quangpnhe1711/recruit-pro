using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RecruitPro.Application.Configurations;

namespace RecruitPro.Infrastructure.Service;

internal static class AiCompatibleApiHelper
{
    private const string ResponsesSuffix = "/responses";
    private const string ChatCompletionsSuffix = "/chat/completions";

    public static HttpRequestMessage BuildRequest(
        AiProviderSettings settings,
        object requestBody,
        JsonSerializerOptions jsonOptions)
    {
        HttpRequestMessage request = new(HttpMethod.Post, BuildEndpoint(settings))
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, jsonOptions), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        return request;
    }

    public static string BuildEndpoint(AiProviderSettings settings)
    {
        string baseUrl = settings.BaseUrl.TrimEnd('/');
        return UsesChatCompletions(settings)
            ? $"{baseUrl}{ChatCompletionsSuffix}"
            : $"{baseUrl}{ResponsesSuffix}";
    }

    public static bool UsesChatCompletions(AiProviderSettings settings)
    {
        return settings.BaseUrl.Contains("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase)
            || settings.BaseUrl.EndsWith("/openai", StringComparison.OrdinalIgnoreCase);
    }

    public static string? ExtractOutputText(string rawResponse)
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

    public static string NormalizeJsonPayload(string payload)
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
}
