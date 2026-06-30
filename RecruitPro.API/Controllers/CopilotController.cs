using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RecruitPro.API.Extensions;
using RecruitPro.Application.DTOs.Request.Copilot;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

// Phase 2.2b: SystemAdmin removed. Copilot is a business-data tool (candidate ranking, job criteria,
// conversation history) restricted to HR/Manager business roles. Job-ownership check for the
// candidate pool endpoint is enforced in the service layer.
[ApiController]
[Authorize(Roles = "HR,Manager")]
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
        var result = await _copilotService.GetCandidatePoolAsync(jobId, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/copilot/candidate-search")]
    public async Task<IActionResult> SearchCandidates([FromBody] NaturalLanguageCandidateSearchRequest request)
    {
        var result = await _copilotService.SearchCandidatesAsync(request, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/copilot/jobs/{jobId:guid}/fit-analysis")]
    public async Task<IActionResult> AnalyzeCandidateFit(Guid jobId, [FromBody] CandidateFitAnalysisRequest request)
    {
        var result = await _copilotService.AnalyzeCandidateFitAsync(jobId, request, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/copilot/jobs/{jobId:guid}/interview-questions")]
    public async Task<IActionResult> GenerateInterviewQuestions(Guid jobId, [FromBody] InterviewQuestionRequest request)
    {
        var result = await _copilotService.GenerateInterviewQuestionsAsync(jobId, request, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/copilot/jobs/{jobId:guid}/shortlists")]
    public async Task<IActionResult> GenerateShortlist(Guid jobId, [FromBody] ShortlistRequest request)
    {
        var result = await _copilotService.GenerateShortlistAsync(jobId, request, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/copilot/applications/{applicationId:guid}/emails/draft")]
    public async Task<IActionResult> DraftApplicationEmail(Guid applicationId, [FromBody] HrEmailDraftRequest request)
    {
        var result = await _copilotService.DraftApplicationEmailAsync(applicationId, request, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/copilot/prompt-templates")]
    public async Task<IActionResult> GetPromptTemplates()
    {
        var result = await _copilotService.GetPromptTemplatesAsync(User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/copilot/prompt-templates")]
    public async Task<IActionResult> CreatePromptTemplate([FromBody] CreateCopilotPromptTemplateRequest request)
    {
        var result = await _copilotService.CreatePromptTemplateAsync(request, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/copilot/applications/{applicationId:guid}/fit-analysis/latest")]
    public async Task<IActionResult> GetLatestFitAnalysis(Guid applicationId)
    {
        var result = await _copilotService.GetLatestFitAnalysisForApplicationAsync(applicationId, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/copilot/artifacts")]
    public async Task<IActionResult> GetGeneratedArtifacts(
        [FromQuery] Guid? jobId,
        [FromQuery] Guid? applicationId,
        [FromQuery] string? artifactType,
        [FromQuery] int take = 20)
    {
        var result = await _copilotService.GetGeneratedArtifactsAsync(User.GetCurrentUserId(), jobId, applicationId, artifactType, take);
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
