using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers
{
    [Route("api/jobs")]
    [ApiController]
    public class JobController : ControllerBase
    {
        private readonly IJobService _jobService;

        public JobController(IJobService jobService)
        {
            _jobService = jobService;
        }

        [HttpGet]
        public async Task<IActionResult> GetJobs([FromQuery] JobQueryRequest request)
        {
            var result = await _jobService.SearchJobsAsync(request);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters()
        {
            var result = await _jobService.GetFiltersAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{jobId}")]
        public async Task<IActionResult> GetJobDetail(string jobId)
        {
            var result = await _jobService.GetJobScreenDetailAsync(jobId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{jobId}/applications")]
        public async Task<IActionResult> GetJobApplications(string jobId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _jobService.GetJobApplicationsAsync(jobId, page, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{jobId}/applications/recent")]
        public async Task<IActionResult> GetRecentApplications(string jobId)
        {
            var result = await _jobService.GetRecentApplicationsAsync(jobId);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Candidate")]
        [HttpPost("{jobId}/apply")]
        public async Task<IActionResult> Apply(string jobId, [FromBody] ApplyJobRequest request)
        {
            var result = await _jobService.ApplyAsync(GetCurrentUserId(), jobId, request);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{jobId}/statistics")]
        public async Task<IActionResult> GetJobStatistics(string jobId)
        {
            var result = await _jobService.GetJobStatisticsAsync(jobId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPatch("{jobId}/status")]
        public async Task<IActionResult> UpdateJobStatus(string jobId, [FromBody] UpdateJobStatusRequest request)
        {
            var result = await _jobService.UpdateJobStatusAsync(jobId, request);
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
}
