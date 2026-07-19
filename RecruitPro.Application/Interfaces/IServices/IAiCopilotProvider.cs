using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IServices;

public interface IAiCopilotProvider
{
    Task<AiStructuredJsonResult> TryCreateStructuredJsonAsync(
        string actionType,
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);

    Task<CopilotPromptResponseDto?> TryCreateRankingAsync(
        CopilotCandidatePoolDto pool,
        CopilotNormalizedRulesDto rules,
        IReadOnlyList<CopilotRankingResultDto> deterministicResults,
        string userPrompt,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Non-ranking chat reply. Scope (v2 §14) is the AI's own judgment call from the job/JD/CV context,
    /// <paramref name="history"/>, and <paramref name="userPrompt"/> — the backend does not pre-filter
    /// by keyword. <paramref name="history"/> is the prior turns of this conversation (oldest first),
    /// letting the AI resolve short follow-ups (e.g. "gợi ý thêm") against what was already discussed.
    /// </summary>
    Task<string?> TryCreateChatReplyAsync(
        CopilotCandidatePoolDto pool,
        string userPrompt,
        IReadOnlyList<CopilotMessage> history,
        Guid conversationId,
        CancellationToken cancellationToken = default);
}

public sealed class AiStructuredJsonResult
{
    private AiStructuredJsonResult(
        bool succeeded,
        string? json,
        string providerName,
        string modelName,
        string? failureReason,
        int? promptTokens = null,
        int? completionTokens = null,
        int? totalTokens = null)
    {
        Succeeded = succeeded;
        Json = json;
        ProviderName = providerName;
        ModelName = modelName;
        FailureReason = failureReason;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        TotalTokens = totalTokens;
    }

    public bool Succeeded { get; }
    public string? Json { get; }
    public string ProviderName { get; }
    public string ModelName { get; }
    public string? FailureReason { get; }

    // v5.1 — provider-reported token usage, when available. Null when the provider omits a usage block.
    public int? PromptTokens { get; }
    public int? CompletionTokens { get; }
    public int? TotalTokens { get; }

    public static AiStructuredJsonResult Success(
        string json, string providerName, string modelName,
        int? promptTokens = null, int? completionTokens = null, int? totalTokens = null)
        => new(true, json, providerName, modelName, null, promptTokens, completionTokens, totalTokens);

    public static AiStructuredJsonResult Failure(string failureReason, string providerName = "", string modelName = "")
        => new(false, null, providerName, modelName, failureReason);
}
