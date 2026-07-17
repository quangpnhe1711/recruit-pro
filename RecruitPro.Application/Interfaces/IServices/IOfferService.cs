using RecruitPro.Application.DTOs.Request.Offers;
using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface IOfferService
{
    Task<ApiResponse<ApplicationOfferEditorDto>> GetOfferEditorAsync(string applicationId, Guid? callerUserId, IReadOnlyCollection<string> callerRoles);
    Task<ApiResponse<ApplicationOfferEditorDto>> SaveDraftAsync(string applicationId, Guid? actorId, IReadOnlyCollection<string> callerRoles, UpsertApplicationOfferRequest request);
    Task<ApiResponse<ApplicationOfferEditorDto>> SendOfferAsync(string applicationId, Guid? actorId, IReadOnlyCollection<string> callerRoles, UpsertApplicationOfferRequest request);
    Task<ApiResponse<CandidateOfferViewDto>> GetCandidateOfferAsync(string applicationId, Guid? callerUserId);
}
