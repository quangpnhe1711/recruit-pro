using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
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
        var result = await _applicationService.ApplyAsync(GetCurrentUserId(), jobId, request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/candidate/applications")]
    public async Task<IActionResult> GetCandidateApplications([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? status = null, [FromQuery] string? keyword = null)
    {
        var result = await _applicationService.GetCandidateApplicationsAsync(GetCurrentUserId(), page, pageSize, status, keyword);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/applications/{applicationId}/withdraw")]
    public async Task<IActionResult> WithdrawApplication(string applicationId)
    {
        var result = await _applicationService.WithdrawApplicationAsync(GetCurrentUserId(), applicationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/applications/{applicationId}/accept-offer")]
    public async Task<IActionResult> AcceptOffer(string applicationId)
    {
        var result = await _applicationService.AcceptOfferAsync(GetCurrentUserId(), applicationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/applications")]
    public async Task<IActionResult> GetHrApplications([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] string? department = null, [FromQuery] string? status = null)
    {
        var result = await _applicationService.GetHrApplicationsAsync(page, pageSize, keyword, department, status);
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

    private Guid GetCurrentUserId()
    {
        string sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("Missing user id claim.");
        return Guid.Parse(sub);
    }
}
