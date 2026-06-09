using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface IInterviewService
{
    Task<ApiResponse<InterviewListResponseDto>> GetInterviewsAsync(int page, int pageSize, string? keyword, string? status, DateTime? startDate, DateTime? endDate);
    Task<ApiResponse<ScheduleDataResponseDto>> GetScheduleDataAsync(string? applicationId = null);
    Task<ApiResponse<InterviewCreatedResponseDto>> CreateInterviewAsync(CreateInterviewRequest request);
    Task<ApiResponse<string>> UpdateInterviewStatusAsync(string interviewId, UpdateInterviewStatusRequest request);
    Task<ApiResponse<string>> DeleteInterviewAsync(string interviewId);
}
