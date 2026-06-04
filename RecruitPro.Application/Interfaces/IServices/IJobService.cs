using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Request.Jobs;

namespace RecruitPro.Application.Interfaces.IServices
{
    public interface IJobService
    {
        public Task<ApiResponse<JobsListingResponseDto>> GetJobsAsync(int currentPage = 1, int pageSize = 10);
        public Task<ApiResponse<JobDetailDto>> GetJobDetailAsync(string jobId);
        public Task<ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>> GetJobApplicationsAsync(string jobId, int page = 1, int pageSize = 10);
        public Task<ApiResponse<HiringFunnelStatisticsDto>> GetJobStatisticsAsync(string jobId);
        public Task<ApiResponse<JobDetailDto>> UpdateJobStatusAsync(string jobId, UpdateJobStatusRequest request);
        public Task<ApiResponse<JobSearchResponseDto>> SearchJobsAsync(JobQueryRequest request);
        public Task<ApiResponse<JobFiltersResponseDto>> GetFiltersAsync();
        public Task<ApiResponse<JobDetailScreenDto>> GetJobScreenDetailAsync(string jobId);
        public Task<ApiResponse<IReadOnlyList<RecentJobApplicationDto>>> GetRecentApplicationsAsync(string jobId);
        public Task<ApiResponse<ApplyJobResponseDto>> ApplyAsync(Guid userId, string jobId, ApplyJobRequest request);
    }
}
