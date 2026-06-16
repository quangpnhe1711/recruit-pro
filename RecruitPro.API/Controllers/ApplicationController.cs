using RecruitPro.Application.DTOs.Request.Applications;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.API.Extensions;
using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
public class ApplicationController : ControllerBase
{
    private readonly IApplicationService _applicationService;

    public ApplicationController(IApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    [HttpGet("api/jobs/{jobId}/apply-context")]
    public async Task<IActionResult> GetApplyContext(string jobId)
    {
        var result = await _applicationService.GetApplyScreenAsync(User.GetCurrentUserId(), jobId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/jobs/{jobId}/applications")]
    public async Task<IActionResult> GetJobApplications(string jobId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _applicationService.GetJobApplicationsAsync(jobId, page, pageSize);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/jobs/{jobId}/applications/recent")]
    public async Task<IActionResult> GetRecentApplications(string jobId)
    {
        var result = await _applicationService.GetRecentApplicationsAsync(jobId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/jobs/{jobId}/apply")]
    public async Task<IActionResult> Apply(string jobId, [FromBody] ApplyJobRequest request)
    {
        var result = await _applicationService.ApplyAsync(User.GetCurrentUserId(), jobId, request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/candidate/applications")]
    public async Task<IActionResult> GetCandidateApplications([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? status = null, [FromQuery] string? keyword = null)
    {
        var result = await _applicationService.GetCandidateApplicationsAsync(User.GetCurrentUserId(), page, pageSize, status, keyword);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/applications/{applicationId}/withdraw")]
    public async Task<IActionResult> WithdrawApplication(string applicationId)
    {
        var result = await _applicationService.WithdrawApplicationAsync(User.GetCurrentUserId(), applicationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/applications/{applicationId}/accept-offer")]
    public async Task<IActionResult> AcceptOffer(string applicationId)
    {
        var result = await _applicationService.AcceptOfferAsync(User.GetCurrentUserId(), applicationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/applications/{applicationId}/decline-offer")]
    public async Task<IActionResult> DeclineOffer(string applicationId)
    {
        var result = await _applicationService.DeclineOfferAsync(User.GetCurrentUserId(), applicationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/applications")]
    public async Task<IActionResult> GetHrApplications([FromQuery] HrApplicationQueryRequest request)
    {
        var result = await _applicationService.GetHrApplicationsAsync(
            request.Page,
            request.PageSize,
            request.Keyword,
            request.Department,
            request.Status,
            request.JobId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/manager/applications/review-queue")]
    public async Task<IActionResult> GetManagerReviewQueue([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null)
    {
        var result = await _applicationService.GetManagerReviewQueueAsync(page, pageSize, keyword);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/applications/{applicationId}")]
    public async Task<IActionResult> GetApplicationReviewDetail(string applicationId)
    {
        var result = await _applicationService.GetApplicationReviewDetailAsync(applicationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/hr/applications/{applicationId}/decision")]
    public async Task<IActionResult> UpdateApplicationDecision(string applicationId, [FromBody] UpdateApplicationDecisionRequest request)
    {
        var result = await _applicationService.UpdateApplicationDecisionAsync(applicationId, User.TryGetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/applications/{applicationId}/cv")]
    public async Task<IActionResult> GetApplicationCv(string applicationId)
    {
        var result = await _applicationService.GetApplicationCvAsync(applicationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/hr/applications/{applicationId}/send-email")]
    public async Task<IActionResult> SendApplicationEmail(string applicationId, [FromBody] SendApplicationEmailRequest request)
    {
        var result = await _applicationService.SendApplicationEmailAsync(applicationId, request);
        return StatusCode(result.StatusCode, result);
    }

}
