using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface IJobService
{
    Task<ApiResponse<JobsListingResponseDto>> GetJobsAsync(int currentPage = 1, int pageSize = 10);
    Task<ApiResponse<JobDetailResponseDto>> GetJobDetailAsync(string jobId);
    Task<ApiResponse<HiringFunnelStatisticsDto>> GetJobStatisticsAsync(string jobId);
    Task<ApiResponse<JobDetailResponseDto>> UpdateJobStatusAsync(string jobId, UpdateJobStatusRequest request);
    Task<ApiResponse<JobSearchResponseDto>> SearchJobsAsync(JobQueryRequest request);
    Task<ApiResponse<JobFiltersResponseDto>> GetFiltersAsync();
    Task<ApiResponse<IReadOnlyList<DepartmentDto>>> GetDepartmentsAsync();
    Task<ApiResponse<IReadOnlyList<SkillLookupDto>>> GetSkillsAsync();
    Task<ApiResponse<JobDetailScreenDto>> GetJobScreenDetailAsync(string jobId);
    Task<ApiResponse<HrJobsResponseDto>> GetHrJobsAsync(HrJobQueryRequest request, Guid currentUserId);
    Task<ApiResponse<ManagerJobApprovalQueueResponseDto>> GetManagerApprovalQueueAsync(ManagerJobApprovalQueryRequest request);
    Task<ApiResponse<ManagerJobApprovalDetailDto>> GetManagerApprovalDetailAsync(string jobId);
    Task<ApiResponse<HrCreateJobResponseDto>> CreateJobAsync(CreateJobRequest request, Guid currentUserId);
    Task<ApiResponse<HrJobStatusResponseDto>> PatchJobAsync(string jobId, PatchJobRequest request);
    Task<ApiResponse<string>> DeleteJobAsync(string jobId);
}
