using RecruitPro.Application.DTOs.Response.Copilot;

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

    Task<string?> TryCreateChatReplyAsync(
        CopilotCandidatePoolDto pool,
        string userPrompt,
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
        string? failureReason)
    {
        Succeeded = succeeded;
        Json = json;
        ProviderName = providerName;
        ModelName = modelName;
        FailureReason = failureReason;
    }

    public bool Succeeded { get; }
    public string? Json { get; }
    public string ProviderName { get; }
    public string ModelName { get; }
    public string? FailureReason { get; }

    public static AiStructuredJsonResult Success(string json, string providerName, string modelName)
        => new(true, json, providerName, modelName, null);

    public static AiStructuredJsonResult Failure(string failureReason, string providerName = "", string modelName = "")
        => new(false, null, providerName, modelName, failureReason);
}
