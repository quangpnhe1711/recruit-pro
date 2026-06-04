using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[Authorize(Roles = "HR,Manager,SystemAdmin")]
[Route("api/hr/candidates")]
[ApiController]
public class HrCandidatesController : ControllerBase
{
    private readonly IHrService _hrService;

    public HrCandidatesController(IHrService hrService)
    {
        _hrService = hrService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCandidates([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] string? status = null, [FromQuery] string? source = null)
    {
        var result = await _hrService.GetCandidatesAsync(page, pageSize, keyword, status, source);
        return StatusCode(result.StatusCode, result);
    }
}
