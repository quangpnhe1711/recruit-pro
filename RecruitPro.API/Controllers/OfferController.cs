using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RecruitPro.API.Extensions;
using RecruitPro.Application.DTOs.Request.Offers;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
public class OfferController : ControllerBase
{
    private readonly IOfferService _offerService;

    public OfferController(IOfferService offerService)
    {
        _offerService = offerService;
    }

    // Phase 2.2b: SystemAdmin removed. Offer operations are HR/Manager business actions only.
    // Ownership check (caller must own the underlying application) is enforced in the service.
    [HttpGet("api/hr/applications/{applicationId}/offer")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> GetOfferEditor(string applicationId)
    {
        var result = await _offerService.GetOfferEditorAsync(applicationId, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("api/hr/applications/{applicationId}/offer")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> SaveOfferDraft(string applicationId, [FromBody] UpsertApplicationOfferRequest request)
    {
        var result = await _offerService.SaveDraftAsync(applicationId, User.TryGetCurrentUserId(), User.GetRoles(), request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/hr/applications/{applicationId}/offer/send")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> SendOffer(string applicationId, [FromBody] UpsertApplicationOfferRequest request)
    {
        var result = await _offerService.SendOfferAsync(applicationId, User.TryGetCurrentUserId(), User.GetRoles(), request);
        return StatusCode(result.StatusCode, result);
    }
}
