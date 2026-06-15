using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories;

public interface ICopilotRepository
{
    Task<IReadOnlyList<CopilotJobOptionDto>> GetJobOptionsAsync();
    Task<CopilotCandidatePoolDto?> GetCandidatePoolAsync(Guid jobId);
    Task<CopilotConversation?> GetConversationAsync(Guid conversationId);
    Task<CopilotConversation?> GetConversationWithDetailsAsync(Guid conversationId);
    Task<CopilotConversation?> GetLatestConversationAsync(Guid jobId, Guid userId);
    Task<CopilotRankingSession?> GetRankingSessionAsync(Guid rankingSessionId);
    Task<IReadOnlyList<CopilotSavedRule>> GetSavedRulesAsync(Guid jobId, Guid userId);
    Task<CopilotSavedRule?> GetSavedRuleAsync(Guid ruleId, Guid userId);
    Task<int> GetNextMessageSequenceAsync(Guid conversationId);
    Task AddConversationAsync(CopilotConversation conversation);
    Task AddMessageAsync(CopilotMessage message);
    Task AddRankingSessionAsync(CopilotRankingSession session);
    Task AddSavedRuleAsync(CopilotSavedRule rule);
}
