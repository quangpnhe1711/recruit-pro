using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.Interfaces;

namespace RecruitPro.Infrastructure.Service;

public class AiEmbeddingProvider : IEmbeddingProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly AiProviderSettings _settings;
    private readonly ILogger<AiEmbeddingProvider> _logger;

    public AiEmbeddingProvider(
        HttpClient httpClient,
        IOptions<AiProviderSettings> options,
        ILogger<AiEmbeddingProvider> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<EmbeddingGenerationResult> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(text))
        {
            return new EmbeddingGenerationResult
            {
                Succeeded = false,
                ModelName = _settings.EmbeddingModel,
                FailureReason = !_settings.Enabled
                    ? "Embedding generation is disabled in configuration."
                    : string.IsNullOrWhiteSpace(_settings.ApiKey)
                        ? "Embedding API key is missing."
                        : "Embedding text is empty."
            };
        }

        using HttpRequestMessage request = new(HttpMethod.Post, BuildEmbeddingsEndpoint(_settings))
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                model = _settings.EmbeddingModel,
                input = text
            }, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string raw = await response.Content.ReadAsStringAsync(cancellationToken);
            string? traceId = response.Headers.TryGetValues("x-request-id", out IEnumerable<string>? requestIds)
                ? requestIds.FirstOrDefault()
                : response.Headers.TryGetValues("request-id", out IEnumerable<string>? altRequestIds)
                    ? altRequestIds.FirstOrDefault()
                    : null;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Embedding request failed. Provider={Provider}, Model={Model}, StatusCode={StatusCode}, TraceId={TraceId}, Response={Response}",
                    "AiCompatible",
                    _settings.EmbeddingModel,
                    (int)response.StatusCode,
                    traceId,
                    raw);

                return new EmbeddingGenerationResult
                {
                    Succeeded = false,
                    ModelName = _settings.EmbeddingModel,
                    FailureReason = $"Embedding provider returned HTTP {(int)response.StatusCode}.",
                    HttpStatusCode = (int)response.StatusCode,
                    RawProviderResponse = raw,
                    TraceId = traceId,
                    IsRetryable = IsRetryableStatusCode((int)response.StatusCode)
                };
            }

            using JsonDocument document = JsonDocument.Parse(raw);
            if (!document.RootElement.TryGetProperty("data", out JsonElement data)
                || data.ValueKind != JsonValueKind.Array
                || data.GetArrayLength() == 0)
            {
                return new EmbeddingGenerationResult
                {
                    Succeeded = false,
                    ModelName = _settings.EmbeddingModel,
                    FailureReason = "Embedding provider returned no vector data."
                };
            }

            JsonElement first = data[0];
            if (!first.TryGetProperty("embedding", out JsonElement embedding) || embedding.ValueKind != JsonValueKind.Array)
            {
                return new EmbeddingGenerationResult
                {
                    Succeeded = false,
                    ModelName = _settings.EmbeddingModel,
                    FailureReason = "Embedding provider returned an invalid vector payload."
                };
            }

            List<double> vector = embedding.EnumerateArray().Select(item => item.GetDouble()).ToList();
            return new EmbeddingGenerationResult
            {
                Succeeded = true,
                ModelName = _settings.EmbeddingModel,
                Vector = vector
            };
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Embedding request timed out.");
            return new EmbeddingGenerationResult
            {
                Succeeded = false,
                ModelName = _settings.EmbeddingModel,
                FailureReason = "Embedding generation timed out.",
                IsRetryable = true
            };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Embedding request failed.");
            return new EmbeddingGenerationResult
            {
                Succeeded = false,
                ModelName = _settings.EmbeddingModel,
                FailureReason = exception.Message
            };
        }
    }

    private static string BuildEmbeddingsEndpoint(AiProviderSettings settings)
    {
        return settings.BaseUrl.TrimEnd('/') + "/embeddings";
    }

    private static bool IsRetryableStatusCode(int statusCode)
    {
        return statusCode is 429 or 500 or 502 or 503 or 504;
    }
}
