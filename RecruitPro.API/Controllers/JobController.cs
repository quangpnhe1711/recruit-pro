using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
public class JobController : ControllerBase
{
    private readonly IJobService _jobService;

    public JobController(IJobService jobService)
    {
        _jobService = jobService;
    }

    [HttpGet("api/jobs")]
    public async Task<IActionResult> GetJobs([FromQuery] JobQueryRequest request)
    {
        var result = await _jobService.SearchJobsAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/jobs/filters")]
    public async Task<IActionResult> GetFilters()
    {
        var result = await _jobService.GetFiltersAsync();
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/jobs/{jobId}")]
    public async Task<IActionResult> GetJobDetail(string jobId)
    {
        var result = await _jobService.GetJobScreenDetailAsync(jobId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/jobs/{jobId}/statistics")]
    public async Task<IActionResult> GetJobStatistics(string jobId)
    {
        var result = await _jobService.GetJobStatisticsAsync(jobId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/jobs/{jobId}/status")]
    public async Task<IActionResult> UpdateJobStatus(string jobId, [FromBody] UpdateJobStatusRequest request)
    {
        var result = await _jobService.UpdateJobStatusAsync(jobId, request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/jobs")]
    public async Task<IActionResult> GetHrJobs([FromQuery] HrJobQueryRequest request)
    {
        var result = await _jobService.GetHrJobsAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/hr/jobs")]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobRequest request)
    {
        var result = await _jobService.CreateJobAsync(request, GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/hr/jobs/{jobId}")]
    public async Task<IActionResult> PatchJob(string jobId, [FromBody] PatchJobRequest request)
    {
        var result = await _jobService.PatchJobAsync(jobId, request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/hr/jobs/{jobId}/status")]
    public async Task<IActionResult> PatchHrJobStatus(string jobId, [FromBody] UpdateJobStatusRequest request)
    {
        var result = await _jobService.PatchJobAsync(jobId, new PatchJobRequest
        {
            ApprovalStatus = request.Status
        });
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("api/hr/jobs/{jobId}")]
    public async Task<IActionResult> DeleteJob(string jobId)
    {
        var result = await _jobService.DeleteJobAsync(jobId);
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
