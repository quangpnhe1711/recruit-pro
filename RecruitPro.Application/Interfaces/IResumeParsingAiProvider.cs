using RecruitPro.Application.DTOs.Response;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces;

public interface IResumeParsingAiProvider
{
    Task<ResumeParsingAiResult> TryParseResumeAsync(
        string extractedText,
        IReadOnlyList<Skill> availableSkills,
        CancellationToken cancellationToken = default);
}

public class ResumeParsingAiResult
{
    public bool UsedAi { get; set; }
    public string Provider { get; set; } = "AiCompatible";
    public string? ModelName { get; set; }
    public string? FailureReason { get; set; }
    public CandidateResumeAiParseDto? Data { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? RawProviderResponse { get; set; }
    public string? TraceId { get; set; }
    public bool IsRetryable { get; set; }

    // v5.1 — provider-reported token usage, when available.
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
}
