using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface IHrService
{
    Task<ApiResponse<HrDashboardDto>> GetDashboardAsync();
    Task<ApiResponse<HrJobsResponseDto>> GetJobsAsync(HrJobQueryRequest request);
    Task<ApiResponse<HrCreateJobResponseDto>> CreateJobAsync(CreateJobRequest request, Guid currentUserId);
    Task<ApiResponse<HrJobStatusResponseDto>> PatchJobAsync(string jobId, PatchJobRequest request);
    Task<ApiResponse<string>> DeleteJobAsync(string jobId);
    Task<ApiResponse<HrCandidatesResponseDto>> GetCandidatesAsync(int page, int pageSize, string? keyword, string? status, string? source);
    Task<ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>> GetApplicationsAsync(int page, int pageSize, string? keyword, string? department, string? status);
    Task<ApiResponse<ResumeFileResponseDto>> GetApplicationCvAsync(string applicationId);
    Task<ApiResponse<InterviewListResponseDto>> GetInterviewsAsync(int page, int pageSize, string? keyword, string? status, DateTime? startDate, DateTime? endDate);
    Task<ApiResponse<ScheduleDataResponseDto>> GetScheduleDataAsync();
    Task<ApiResponse<InterviewCreatedResponseDto>> CreateInterviewAsync(CreateInterviewRequest request);
    Task<ApiResponse<string>> UpdateInterviewStatusAsync(string interviewId, UpdateInterviewStatusRequest request);
    Task<ApiResponse<string>> DeleteInterviewAsync(string interviewId);
}
