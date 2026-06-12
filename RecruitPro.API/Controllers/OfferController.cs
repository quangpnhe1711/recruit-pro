using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request.Offers;
using RecruitPro.Application.Interfaces.IServices;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
        var result = await _offerService.SaveDraftAsync(applicationId, TryGetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/hr/applications/{applicationId}/offer/send")]
    public async Task<IActionResult> SendOffer(string applicationId, [FromBody] UpsertApplicationOfferRequest request)
    {
        var result = await _offerService.SendOfferAsync(applicationId, TryGetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }

    private Guid? TryGetCurrentUserId()
    {
        string? sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(sub, out Guid userId) ? userId : null;
    }
}
