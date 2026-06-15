using RecruitPro.Application.DTOs.Response.Copilot;

namespace RecruitPro.Application.Interfaces.IServices;

public interface IAiCopilotProvider
{
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
