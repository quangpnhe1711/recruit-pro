using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request.Copilot;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
public class CopilotController : ControllerBase
{
    private readonly ICopilotService _copilotService;

    public CopilotController(ICopilotService copilotService)
    {
        _copilotService = copilotService;
    }

    [HttpGet("api/copilot/jobs")]
    public async Task<IActionResult> GetJobs()
    {
        var result = await _copilotService.GetJobsAsync();
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/copilot/conversations")]
    public async Task<IActionResult> CreateConversation([FromBody] CreateCopilotConversationRequest request)
    {
        var result = await _copilotService.CreateConversationAsync(request, GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/copilot/jobs/{jobId:guid}/candidates")]
    public async Task<IActionResult> GetCandidatePool(Guid jobId)
    {
        var result = await _copilotService.GetCandidatePoolAsync(jobId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/copilot/conversations/{conversationId:guid}/rankings")]
    public async Task<IActionResult> CreateRanking(Guid conversationId, [FromBody] CopilotPromptRequest request)
    {
        var result = await _copilotService.CreateRankingAsync(conversationId, request, GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    private Guid GetCurrentUserId()
    {
        string sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("Missing user id claim.");
        return Guid.Parse(sub);
    }
}
