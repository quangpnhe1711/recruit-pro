using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[Authorize(Roles = "HR,Manager,SystemAdmin")]
[Route("api/hr/dashboard")]
[ApiController]
public class HrDashboardController : ControllerBase
{
    private readonly IHrService _hrService;

    public HrDashboardController(IHrService hrService)
    {
        _hrService = hrService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _hrService.GetDashboardAsync();
        return StatusCode(result.StatusCode, result);
    }
}
