using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Request.Candidate;
using System.IO;

namespace RecruitPro.Application.Interfaces.IServices
{
    public interface ICandidateProfileService
    {
        Task<ApiResponse<CandidateRegisterResponseDto>> RegisterAsync(CandidateRegisterRequest request, Stream? resumeStream, string? resumeFileName, string? resumeContentType = null);
        Task<ApiResponse<CandidateDashboardDto>> GetDashboardAsync(Guid userId);
        Task<ApiResponse<CandidateApplicationsResponseDto>> GetApplicationsAsync(Guid userId, int page, int pageSize, string? status, string? keyword);
        Task<ApiResponse<string>> WithdrawApplicationAsync(Guid userId, string applicationId);
        Task<ApiResponse<CandidateProfileResponseDto>> GetProfileAsync(Guid userId);
        Task<ApiResponse<CandidateProfileResponseDto>> UpdateProfileAsync(Guid userId, UpdateCandidateProfileRequest request);
        Task<ApiResponse<CandidateProfileResponseDto>> UpdateSkillsAsync(Guid userId, UpdateCandidateSkillsRequest request);
        Task<ApiResponse<ResumeUploadResponseDto>> UploadResumeAsync(Guid userId, Stream resumeStream, string fileName, string contentType);
    }
}
