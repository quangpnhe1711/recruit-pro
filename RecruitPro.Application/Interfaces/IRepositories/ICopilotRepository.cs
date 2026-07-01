using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories;

public interface ICopilotRepository
{
    Task<IReadOnlyList<CopilotJobOptionDto>> GetJobOptionsAsync(Guid callerUserId);
    Task<CopilotCandidatePoolDto?> GetCandidatePoolAsync(Guid jobId);
    Task<CopilotConversation?> GetConversationAsync(Guid conversationId);
    Task<CopilotConversation?> GetConversationWithDetailsAsync(Guid conversationId);
    Task<CopilotConversation?> GetLatestConversationAsync(Guid jobId, Guid userId);
    Task<CopilotRankingSession?> GetRankingSessionAsync(Guid rankingSessionId);
    // v2: latest completed ranking session whose effective-input fingerprint matches — used to
    // suppress duplicate ranking runs/AI calls for unchanged criteria/context.
    Task<CopilotRankingSession?> GetLatestMatchingRankingSessionAsync(Guid jobId, Guid userId, string effectivePayloadHash);
    // v2: latest ranking session for a job owned by the user — fit-analysis and shortlist derive from
    // this instead of re-running an independent ranking pipeline.
    Task<CopilotRankingSession?> GetLatestRankingSessionForJobAsync(Guid jobId, Guid userId);
    Task<IReadOnlyList<CopilotSavedRule>> GetSavedRulesAsync(Guid jobId, Guid userId);
    Task<CopilotSavedRule?> GetSavedRuleAsync(Guid ruleId, Guid userId);
    Task<IReadOnlyList<CopilotPromptTemplate>> GetPromptTemplatesAsync(Guid ownerUserId);
    Task<CandidateFitAnalysis?> GetLatestFitAnalysisAsync(Guid applicationId);
    Task<IReadOnlyList<CopilotGeneratedArtifact>> GetGeneratedArtifactsAsync(Guid ownerUserId, Guid? jobId, Guid? applicationId, string? artifactType, int take);
    Task<int> GetNextMessageSequenceAsync(Guid conversationId);
    Task AddConversationAsync(CopilotConversation conversation);
    Task AddMessageAsync(CopilotMessage message);
    Task AddRankingSessionAsync(CopilotRankingSession session);
    Task AddSavedRuleAsync(CopilotSavedRule rule);
    Task AddPromptTemplateAsync(CopilotPromptTemplate template);
    Task AddFitAnalysisAsync(CandidateFitAnalysis analysis);
    Task AddGeneratedArtifactAsync(CopilotGeneratedArtifact artifact);
}
