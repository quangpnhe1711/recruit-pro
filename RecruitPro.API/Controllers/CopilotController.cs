using Microsoft.AspNetCore.Mvc;
using RecruitPro.API.Extensions;
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
        var result = await _copilotService.CreateConversationAsync(request, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/copilot/conversations/{conversationId:guid}")]
    public async Task<IActionResult> GetConversation(Guid conversationId)
    {
        var result = await _copilotService.GetConversationAsync(conversationId, User.GetCurrentUserId());
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
        var result = await _copilotService.CreateRankingAsync(conversationId, request, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/copilot/ranking-sessions/{rankingSessionId:guid}")]
    public async Task<IActionResult> GetRankingSession(Guid rankingSessionId)
    {
        var result = await _copilotService.GetRankingSessionAsync(rankingSessionId, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/copilot/jobs/{jobId:guid}/rules")]
    public async Task<IActionResult> GetSavedRules(Guid jobId)
    {
        var result = await _copilotService.GetSavedRulesAsync(jobId, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/copilot/jobs/{jobId:guid}/rules")]
    public async Task<IActionResult> CreateSavedRule(Guid jobId, [FromBody] CreateCopilotSavedRuleRequest request)
    {
        request.JobId = jobId;
        var result = await _copilotService.CreateSavedRuleAsync(request, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/copilot/rules/{ruleId:guid}")]
    public async Task<IActionResult> UpdateSavedRuleStatus(Guid ruleId, [FromBody] UpdateCopilotSavedRuleStatusRequest request)
    {
        var result = await _copilotService.UpdateSavedRuleStatusAsync(ruleId, request, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("api/copilot/rules/{ruleId:guid}")]
    public async Task<IActionResult> DeleteSavedRule(Guid ruleId)
    {
        var result = await _copilotService.DeleteSavedRuleAsync(ruleId, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }
}
