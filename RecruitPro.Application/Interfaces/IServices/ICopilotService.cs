using RecruitPro.Application.DTOs.Request.Copilot;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Copilot;

namespace RecruitPro.Application.Interfaces.IServices;

public interface ICopilotService
{
    Task<ApiResponse<IReadOnlyList<CopilotJobOptionDto>>> GetJobsAsync();
    Task<ApiResponse<CopilotConversationDto>> CreateConversationAsync(CreateCopilotConversationRequest request, Guid userId);
    Task<ApiResponse<CopilotCandidatePoolDto>> GetCandidatePoolAsync(Guid jobId);
    Task<ApiResponse<CopilotPromptResponseDto>> CreateRankingAsync(Guid conversationId, CopilotPromptRequest request, Guid userId);
}
