using RecruitPro.Application.DTOs.Request.Candidate;
using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface ICandidateService
{
    Task<ApiResponse<CandidateRegisterResponseDto>> RegisterAsync(CandidateRegisterRequest request, Stream? resumeStream, string? resumeFileName, string? resumeContentType = null);
    Task<ApiResponse<CandidateProfileResponseDto>> GetProfileAsync(Guid userId);
    Task<ApiResponse<CandidateProfileResponseDto>> UpdateProfileAsync(Guid userId, UpdateCandidateProfileRequest request);
    Task<ApiResponse<CandidateProfileResponseDto>> SaveProfileAsync(Guid userId, UpdateCandidateProfileRequest request, Stream? resumeStream, string? resumeFileName, string? resumeContentType = null);
    Task<ApiResponse<CandidateProfileResponseDto>> UpdateSkillsAsync(Guid userId, UpdateCandidateSkillsRequest request);
    Task<ApiResponse<CandidateProfileResponseDto>> CreateExperienceAsync(Guid userId, UpsertCandidateExperienceRequest request);
    Task<ApiResponse<CandidateProfileResponseDto>> UpdateExperienceAsync(Guid userId, string experienceId, UpsertCandidateExperienceRequest request);
    Task<ApiResponse<CandidateProfileResponseDto>> DeleteExperienceAsync(Guid userId, string experienceId);
    Task<ApiResponse<CandidateResumeParseResponseDto>> ParseResumeAsync(Guid userId, Stream resumeStream, string fileName, string? contentType = null);
    Task<ApiResponse<ResumeUploadResponseDto>> UploadResumeAsync(Guid userId, Stream resumeStream, string fileName, string contentType);
    Task<ApiResponse<ResumeFileResponseDto>> GetResumeDownloadUrlAsync(string resumeId);
    Task<ApiResponse<HrCandidatesResponseDto>> GetCandidatesAsync(int page, int pageSize, string? keyword, string? status, string? source);
    Task<ApiResponse<HrCandidateDetailDto>> GetCandidateDetailAsync(string candidateId);
    Task<CandidateImportTemplateDto> GenerateImportTemplateAsync();
    Task<ApiResponse<CandidateImportPreviewResponseDto>> PreviewImportAsync(Stream fileStream, string fileName);
    Task<ApiResponse<CandidateImportResultDto>> ImportCandidatesAsync(CandidateImportRequest request);
}
