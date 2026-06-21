namespace RecruitPro.Application.Interfaces;

public interface IEmbeddingProvider
{
    Task<EmbeddingGenerationResult> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}

public class EmbeddingGenerationResult
{
    public bool Succeeded { get; set; }
    public string Provider { get; set; } = "AiCompatible";
    public string? ModelName { get; set; }
    public string? FailureReason { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? RawProviderResponse { get; set; }
    public string? TraceId { get; set; }
    public bool IsRetryable { get; set; }
    public IReadOnlyList<double> Vector { get; set; } = [];
}
