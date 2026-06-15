using Microsoft.AspNetCore.Mvc;
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

    [HttpGet("api/hr/applications/{applicationId}/offer")]
    public async Task<IActionResult> GetOfferEditor(string applicationId)
    {
        var result = await _offerService.GetOfferEditorAsync(applicationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("api/hr/applications/{applicationId}/offer")]
    public async Task<IActionResult> SaveOfferDraft(string applicationId, [FromBody] UpsertApplicationOfferRequest request)
    {
        var result = await _offerService.SaveDraftAsync(applicationId, User.TryGetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/hr/applications/{applicationId}/offer/send")]
    public async Task<IActionResult> SendOffer(string applicationId, [FromBody] UpsertApplicationOfferRequest request)
    {
        var result = await _offerService.SendOfferAsync(applicationId, User.TryGetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }
}
