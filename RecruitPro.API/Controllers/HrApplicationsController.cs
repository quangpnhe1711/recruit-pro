using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[Authorize(Roles = "HR,Manager,SystemAdmin")]
[Route("api/hr/applications")]
[ApiController]
public class HrApplicationsController : ControllerBase
{
    private readonly IHrService _hrService;

    public HrApplicationsController(IHrService hrService)
    {
        _hrService = hrService;
    }

    [HttpGet]
    public async Task<IActionResult> GetApplications([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] string? department = null, [FromQuery] string? status = null)
    {
        var result = await _hrService.GetApplicationsAsync(page, pageSize, keyword, department, status);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{applicationId}/cv")]
    public async Task<IActionResult> GetApplicationCv(string applicationId)
    {
        var result = await _hrService.GetApplicationCvAsync(applicationId);
        return StatusCode(result.StatusCode, result);
    }
}
