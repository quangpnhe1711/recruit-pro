using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
[Route("api")]
public class LookupController : ControllerBase
{
    private readonly IJobService _jobService;

    public LookupController(IJobService jobService)
    {
        _jobService = jobService;
    }

    // NOTE: GET /api/departments moved to DepartmentController (now returns the department head).

    [HttpGet("skills")]
    public async Task<IActionResult> GetSkills()
    {
        var result = await _jobService.GetSkillsAsync();
        return StatusCode(result.StatusCode, result);
    }
}
