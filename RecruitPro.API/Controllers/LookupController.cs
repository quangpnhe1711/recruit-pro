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

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var result = await _jobService.GetDepartmentsAsync();
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("skills")]
    public async Task<IActionResult> GetSkills()
    {
        var result = await _jobService.GetSkillsAsync();
        return StatusCode(result.StatusCode, result);
    }
}
