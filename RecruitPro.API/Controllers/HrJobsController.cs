using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[Authorize(Roles = "HR,Manager,SystemAdmin")]
[Route("api/hr/jobs")]
[ApiController]
public class HrJobsController : ControllerBase
{
    private readonly IHrService _hrService;

    public HrJobsController(IHrService hrService)
    {
        _hrService = hrService;
    }

    [HttpGet]
    public async Task<IActionResult> GetJobs([FromQuery] HrJobQueryRequest request)
    {
        var result = await _hrService.GetJobsAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobRequest request)
    {
        var result = await _hrService.CreateJobAsync(request, GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("{jobId}")]
    public async Task<IActionResult> PatchJob(string jobId, [FromBody] PatchJobRequest request)
    {
        var result = await _hrService.PatchJobAsync(jobId, request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{jobId}")]
    public async Task<IActionResult> DeleteJob(string jobId)
    {
        var result = await _hrService.DeleteJobAsync(jobId);
        return StatusCode(result.StatusCode, result);
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Missing user id claim.");
        return Guid.Parse(sub);
    }
}
