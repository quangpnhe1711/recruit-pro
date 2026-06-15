using Microsoft.AspNetCore.Mvc;
using RecruitPro.API.Extensions;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("api/candidate/dashboard")]
    public async Task<IActionResult> GetCandidateDashboard()
    {
        var result = await _dashboardService.GetCandidateDashboardAsync(User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/dashboard")]
    public async Task<IActionResult> GetHrDashboard()
    {
        var result = await _dashboardService.GetHrDashboardAsync();
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/manager/dashboard")]
    public async Task<IActionResult> GetManagerDashboard()
    {
        var result = await _dashboardService.GetManagerDashboardAsync();
        return StatusCode(result.StatusCode, result);
    }
}
