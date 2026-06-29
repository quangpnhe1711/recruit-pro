using RecruitPro.Application.DTOs.Request.Copilot;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Copilot;

namespace RecruitPro.Application.Interfaces.IServices;

public interface ICopilotService
{
    Task<ApiResponse<IReadOnlyList<CopilotJobOptionDto>>> GetJobsAsync();
    Task<ApiResponse<CopilotConversationDto>> CreateConversationAsync(CreateCopilotConversationRequest request, Guid userId);
    Task<ApiResponse<CopilotConversationDetailDto>> GetConversationAsync(Guid conversationId, Guid userId);
    Task<ApiResponse<CopilotCandidatePoolDto>> GetCandidatePoolAsync(Guid jobId, Guid? callerUserId, IReadOnlyCollection<string> callerRoles);
    Task<ApiResponse<CopilotPromptResponseDto>> CreateRankingAsync(Guid conversationId, CopilotPromptRequest request, Guid userId);
    Task<ApiResponse<CopilotRankingSessionDetailDto>> GetRankingSessionAsync(Guid rankingSessionId, Guid userId);
    Task<ApiResponse<IReadOnlyList<CopilotSavedRuleDto>>> GetSavedRulesAsync(Guid jobId, Guid userId);
    Task<ApiResponse<CopilotSavedRuleDto>> CreateSavedRuleAsync(CreateCopilotSavedRuleRequest request, Guid userId);
    Task<ApiResponse<CopilotSavedRuleDto>> UpdateSavedRuleStatusAsync(Guid ruleId, UpdateCopilotSavedRuleStatusRequest request, Guid userId);
    Task<ApiResponse<object>> DeleteSavedRuleAsync(Guid ruleId, Guid userId);
}
