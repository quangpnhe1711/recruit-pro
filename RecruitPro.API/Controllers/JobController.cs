using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Response;
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
        public async Task<IActionResult> GetJobs([FromQuery] int pageSize = 10, [FromQuery] int currentPage = 1)
        {
            ApiResponse<JobsListingResponseDto> result = await _jobService.GetJobsAsync(currentPage, pageSize);

            return StatusCode(result.StatusCode, result.Success ? result.Data : result.Message);
        }
    }
}
