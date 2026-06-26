using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Request.Departments;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface IJobService
{
    Task<ApiResponse<JobsListingResponseDto>> GetJobsAsync(int currentPage = 1, int pageSize = 10);
    Task<ApiResponse<JobDetailResponseDto>> GetJobDetailAsync(string jobId);
    Task<ApiResponse<HiringFunnelStatisticsDto>> GetJobStatisticsAsync(string jobId);
    Task<ApiResponse<JobSearchResponseDto>> SearchJobsAsync(JobQueryRequest request);
    Task<ApiResponse<JobFiltersResponseDto>> GetFiltersAsync();
    Task<ApiResponse<IReadOnlyList<DepartmentResponseDto>>> GetDepartmentsAsync();
    Task<ApiResponse<DepartmentResponseDto>> GetDepartmentAsync(string departmentId);
    Task<ApiResponse<DepartmentResponseDto>> UpdateDepartmentAsync(string departmentId, UpdateDepartmentRequest request);
    Task<ApiResponse<IReadOnlyList<SkillLookupDto>>> GetSkillsAsync();
    Task<ApiResponse<JobDetailScreenDto>> GetJobScreenDetailAsync(string jobId);
    Task<ApiResponse<HrJobsResponseDto>> GetHrJobsAsync(HrJobQueryRequest request, Guid currentUserId);
    Task<ApiResponse<ManagerJobApprovalQueueResponseDto>> GetManagerApprovalQueueAsync(ManagerJobApprovalQueryRequest request);
    Task<ApiResponse<ManagerJobApprovalDetailDto>> GetManagerApprovalDetailAsync(string jobId);
    Task<ApiResponse<HrCreateJobResponseDto>> CreateJobAsync(CreateJobRequest request, Guid currentUserId);
    Task<ApiResponse<HrJobStatusResponseDto>> PatchJobAsync(string jobId, PatchJobRequest request, Guid? currentUserId = null, IReadOnlyCollection<string>? currentUserRoles = null);
    Task<ApiResponse<string>> DeleteJobAsync(string jobId);
}
