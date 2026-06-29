using RecruitPro.Application.DTOs.Request.Applications;
using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface IApplicationService
{
    Task<ApiResponse<ApplyJobScreenDto>> GetApplyScreenAsync(Guid userId, string jobId);
    Task<ApiResponse<ApplyJobResponseDto>> ApplyAsync(Guid userId, string jobId, ApplyJobRequest request);
    Task<ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>> GetJobApplicationsAsync(string jobId, int page = 1, int pageSize = 10, Guid? currentUserId = null, IReadOnlyCollection<string>? currentUserRoles = null);
    Task<ApiResponse<IReadOnlyList<RecentJobApplicationDto>>> GetRecentApplicationsAsync(string jobId, Guid? currentUserId = null, IReadOnlyCollection<string>? currentUserRoles = null);
    Task<ApiResponse<CandidateApplicationsResponseDto>> GetCandidateApplicationsAsync(Guid userId, int page, int pageSize, string? status, string? keyword);
    Task<ApiResponse<string>> WithdrawApplicationAsync(Guid userId, string applicationId);
    Task<ApiResponse<string>> AcceptOfferAsync(Guid userId, string applicationId);
    Task<ApiResponse<string>> DeclineOfferAsync(Guid userId, string applicationId);
    Task<ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>> GetHrApplicationsAsync(int page, int pageSize, string? keyword, string? department, string? status, string? jobId, Guid? currentUserId = null, IReadOnlyCollection<string>? currentUserRoles = null);
    Task<ApiResponse<ManagerReviewQueueResponseDto>> GetManagerReviewQueueAsync(int page, int pageSize, string? keyword);
    Task<ApiResponse<ApplicationReviewDetailDto>> GetApplicationReviewDetailAsync(string applicationId, Guid? currentUserId = null, IReadOnlyCollection<string>? currentUserRoles = null);
    Task<ApiResponse<ApplicationReviewDetailDto>> UpdateApplicationDecisionAsync(string applicationId, Guid? reviewerId, UpdateApplicationDecisionRequest request);
    Task<ApiResponse<ApplicationReviewDetailDto>> SendRejectionEmailAsync(string applicationId, Guid? reviewerId, SendRejectionEmailRequest request);
    Task<ApiResponse<ResumeFileResponseDto>> GetApplicationCvAsync(string applicationId, Guid? currentUserId = null, IReadOnlyCollection<string>? currentUserRoles = null);
    Task<ApiResponse<string>> SendApplicationEmailAsync(string applicationId, SendApplicationEmailRequest request);
}
