using RecruitPro.Application.DTOs.Request.Applications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> GetApplyContext(string jobId)
    {
        var result = await _applicationService.GetApplyScreenAsync(User.GetCurrentUserId(), jobId);
        return StatusCode(result.StatusCode, result);
    }

    // These two endpoints return candidate PII (full name, avatar, candidate/application ids, status,
    // score). They were previously anonymous, leaking every applicant of any job to the public. They are
    // only consumed by the authenticated internal portal (HR/Manager job-detail screen), so they are now
    // role-restricted. Public/landing pages must use the aggregate-only GET /api/jobs/{jobId}/statistics.
    [HttpGet("api/jobs/{jobId}/applications")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> GetJobApplications(string jobId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _applicationService.GetJobApplicationsAsync(jobId, page, pageSize, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/jobs/{jobId}/applications/recent")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> GetRecentApplications(string jobId)
    {
        var result = await _applicationService.GetRecentApplicationsAsync(jobId, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/jobs/{jobId}/apply")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> Apply(string jobId, [FromBody] ApplyJobRequest request)
    {
        var result = await _applicationService.ApplyAsync(User.GetCurrentUserId(), jobId, request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/candidate/applications")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> GetCandidateApplications([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? status = null, [FromQuery] string? keyword = null)
    {
        var result = await _applicationService.GetCandidateApplicationsAsync(User.GetCurrentUserId(), page, pageSize, status, keyword);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/applications/{applicationId}/withdraw")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> WithdrawApplication(string applicationId)
    {
        var result = await _applicationService.WithdrawApplicationAsync(User.GetCurrentUserId(), applicationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/applications/{applicationId}/accept-offer")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> AcceptOffer(string applicationId)
    {
        var result = await _applicationService.AcceptOfferAsync(User.GetCurrentUserId(), applicationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/applications/{applicationId}/decline-offer")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> DeclineOffer(string applicationId)
    {
        var result = await _applicationService.DeclineOfferAsync(User.GetCurrentUserId(), applicationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/applications")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> GetHrApplications([FromQuery] HrApplicationQueryRequest request)
    {
        var result = await _applicationService.GetHrApplicationsAsync(
            request.Page,
            request.PageSize,
            request.Keyword,
            request.Department,
            request.Status,
            request.JobId,
            User.TryGetCurrentUserId(),
            User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/manager/applications/review-queue")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> GetManagerReviewQueue([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null)
    {
        var result = await _applicationService.GetManagerReviewQueueAsync(page, pageSize, keyword);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/applications/{applicationId}")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> GetApplicationReviewDetail(string applicationId)
    {
        var result = await _applicationService.GetApplicationReviewDetailAsync(applicationId, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/hr/applications/{applicationId}/decision")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> UpdateApplicationDecision(string applicationId, [FromBody] UpdateApplicationDecisionRequest request)
    {
        var result = await _applicationService.UpdateApplicationDecisionAsync(applicationId, User.TryGetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/hr/applications/{applicationId}/rejection-email")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> SendRejectionEmail(string applicationId, [FromBody] SendRejectionEmailRequest request)
    {
        var result = await _applicationService.SendRejectionEmailAsync(applicationId, User.TryGetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/applications/{applicationId}/cv")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> GetApplicationCv(string applicationId)
    {
        var result = await _applicationService.GetApplicationCvAsync(applicationId, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/hr/applications/{applicationId}/send-email")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> SendApplicationEmail(string applicationId, [FromBody] SendApplicationEmailRequest request)
    {
        var result = await _applicationService.SendApplicationEmailAsync(applicationId, request);
        return StatusCode(result.StatusCode, result);
    }

}
