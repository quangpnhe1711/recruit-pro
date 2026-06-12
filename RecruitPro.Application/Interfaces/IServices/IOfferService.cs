using RecruitPro.Application.DTOs.Request.Offers;
using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface IOfferService
{
    Task<ApiResponse<ApplicationOfferEditorDto>> GetOfferEditorAsync(string applicationId);
    Task<ApiResponse<ApplicationOfferEditorDto>> SaveDraftAsync(string applicationId, Guid? actorId, UpsertApplicationOfferRequest request);
    Task<ApiResponse<ApplicationOfferEditorDto>> SendOfferAsync(string applicationId, Guid? actorId, UpsertApplicationOfferRequest request);
}
