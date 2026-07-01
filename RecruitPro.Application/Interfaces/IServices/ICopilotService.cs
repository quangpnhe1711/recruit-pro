using RecruitPro.Application.DTOs.Request.Copilot;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Copilot;

namespace RecruitPro.Application.Interfaces.IServices;

public interface ICopilotService
{
    Task<ApiResponse<IReadOnlyList<CopilotJobOptionDto>>> GetJobsAsync(Guid callerUserId);
    Task<ApiResponse<CopilotConversationDto>> CreateConversationAsync(CreateCopilotConversationRequest request, Guid userId);
    Task<ApiResponse<CopilotConversationDetailDto>> GetConversationAsync(Guid conversationId, Guid userId);
    Task<ApiResponse<CopilotCandidatePoolDto>> GetCandidatePoolAsync(Guid jobId, Guid? callerUserId, IReadOnlyCollection<string> callerRoles);
    Task<ApiResponse<NaturalLanguageCandidateSearchResponseDto>> SearchCandidatesAsync(NaturalLanguageCandidateSearchRequest request, Guid? callerUserId, IReadOnlyCollection<string> callerRoles);
    Task<ApiResponse<CandidateFitAnalysisResponseDto>> AnalyzeCandidateFitAsync(Guid jobId, CandidateFitAnalysisRequest request, Guid? callerUserId, IReadOnlyCollection<string> callerRoles);
    Task<ApiResponse<InterviewQuestionSetDto>> GenerateInterviewQuestionsAsync(Guid jobId, InterviewQuestionRequest request, Guid? callerUserId, IReadOnlyCollection<string> callerRoles);
    Task<ApiResponse<HrEmailDraftResponseDto>> DraftApplicationEmailAsync(Guid applicationId, HrEmailDraftRequest request, Guid? callerUserId, IReadOnlyCollection<string> callerRoles);
    Task<ApiResponse<IReadOnlyList<CopilotPromptTemplateDto>>> GetPromptTemplatesAsync(Guid userId);
    Task<ApiResponse<CopilotPromptTemplateDto>> CreatePromptTemplateAsync(CreateCopilotPromptTemplateRequest request, Guid userId);
    Task<ApiResponse<CandidateFitAnalysisSnapshotDto>> GetLatestFitAnalysisForApplicationAsync(Guid applicationId, Guid? callerUserId, IReadOnlyCollection<string> callerRoles);
    Task<ApiResponse<IReadOnlyList<CopilotGeneratedArtifactDto>>> GetGeneratedArtifactsAsync(Guid ownerUserId, Guid? jobId, Guid? applicationId, string? artifactType, int take);
    Task<ApiResponse<CopilotPromptResponseDto>> CreateRankingAsync(Guid conversationId, CopilotPromptRequest request, Guid userId);
    Task<ApiResponse<PassCvResultDto>> PassCvToHeadReviewAsync(Guid rankingSessionId, PassCvToHeadReviewRequest request, Guid userId, IReadOnlyCollection<string> callerRoles);
    Task<ApiResponse<CopilotRankingSessionDetailDto>> GetRankingSessionAsync(Guid rankingSessionId, Guid userId);
    Task<ApiResponse<IReadOnlyList<CopilotSavedRuleDto>>> GetSavedRulesAsync(Guid jobId, Guid userId);
    Task<ApiResponse<CopilotSavedRuleDto>> CreateSavedRuleAsync(CreateCopilotSavedRuleRequest request, Guid userId);
    Task<ApiResponse<CopilotSavedRuleDto>> UpdateSavedRuleStatusAsync(Guid ruleId, UpdateCopilotSavedRuleStatusRequest request, Guid userId);
    Task<ApiResponse<object>> DeleteSavedRuleAsync(Guid ruleId, Guid userId);
}
