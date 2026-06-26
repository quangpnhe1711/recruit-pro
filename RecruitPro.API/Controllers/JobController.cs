using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RecruitPro.API.Extensions;
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

    // Hardened: this previously-public endpoint now requires auth and routes through the same guarded
    // path as PATCH /api/hr/jobs/{id}/status — approve/reject is scoped to the DepartmentHead or
    // SystemAdmin (BR-OWN-003). It is an alias of the HR status endpoint and returns the same shape.
    [HttpPatch("api/jobs/{jobId}/status")]
    [Authorize(Roles = "HR,Manager,HeadDepartment,SystemAdmin")]
    public async Task<IActionResult> UpdateJobStatus(string jobId, [FromBody] UpdateJobStatusRequest request)
    {
        var result = await _jobService.PatchJobAsync(jobId, new PatchJobRequest
        {
            ApprovalStatus = request.Status
        }, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/jobs")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> GetHrJobs([FromQuery] HrJobQueryRequest request)
    {
        var result = await _jobService.GetHrJobsAsync(request, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/jobs/{jobId}")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> GetHrJobDetail(string jobId)
    {
        var result = await _jobService.GetJobDetailAsync(jobId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/manager/jobs/approval-queue")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> GetManagerApprovalQueue([FromQuery] ManagerJobApprovalQueryRequest request)
    {
        var result = await _jobService.GetManagerApprovalQueueAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/manager/jobs/{jobId}/approval-detail")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> GetManagerApprovalDetail(string jobId)
    {
        var result = await _jobService.GetManagerApprovalDetailAsync(jobId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/hr/jobs")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobRequest request)
    {
        var result = await _jobService.CreateJobAsync(request, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    // SystemAdmin + HeadDepartment are admitted here so the department head / admin can approve/reject;
    // the service guard (BR-OWN-003) restricts the Approved/Rejected transition to the job's department
    // head or a SystemAdmin. General field edits remain available to HR/Manager.
    [HttpPatch("api/hr/jobs/{jobId}")]
    [Authorize(Roles = "HR,Manager,HeadDepartment,SystemAdmin")]
    public async Task<IActionResult> PatchJob(string jobId, [FromBody] PatchJobRequest request)
    {
        var result = await _jobService.PatchJobAsync(jobId, request, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/hr/jobs/{jobId}/status")]
    [Authorize(Roles = "HR,Manager,HeadDepartment,SystemAdmin")]
    public async Task<IActionResult> PatchHrJobStatus(string jobId, [FromBody] UpdateJobStatusRequest request)
    {
        var result = await _jobService.PatchJobAsync(jobId, new PatchJobRequest
        {
            ApprovalStatus = request.Status
        }, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("api/hr/jobs/{jobId}")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> DeleteJob(string jobId)
    {
        var result = await _jobService.DeleteJobAsync(jobId);
        return StatusCode(result.StatusCode, result);
    }
}
