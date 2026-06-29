using RecruitPro.Application.DTOs.Request.Discovery;
using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface ISemanticDiscoveryService
{
    Task<ApiResponse<SemanticCandidatesResponseDto>> SearchTalentPoolAsync(TalentPoolSearchRequest request);
    Task<ApiResponse<SemanticCandidatesResponseDto>> DiscoverCandidatesAsync(CandidateDiscoveryRequest request);
    Task<ApiResponse<SemanticCandidatesResponseDto>> GetSimilarCandidatesAsync(string candidateId, int page, int pageSize);
    Task<ApiResponse<SemanticJobsResponseDto>> GetSimilarJobsAsync(string jobId, int page, int pageSize);
    Task<ApiResponse<SemanticCandidatesResponseDto>> GetRecommendedCandidatesAsync(string jobId, Guid? callerUserId, IReadOnlyCollection<string> callerRoles, int page, int pageSize);
    Task<ApiResponse<IReadOnlyList<RecommendedJobDto>>> GetRecommendedJobsForCandidateAsync(Guid userId, int take);
    Task RefreshCandidateEmbeddingAsync(Guid candidateProfileId);
    Task RefreshJobEmbeddingAsync(Guid jobId);
}
