using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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

    // Phase 2.2b: SystemAdmin removed from all dashboard endpoints (business data surfaces).
    // The HR dashboard is restricted to HR/Manager roles; Manager dashboard to Manager/HeadDepartment.
    [HttpGet("api/candidate/dashboard")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> GetCandidateDashboard()
    {
        var result = await _dashboardService.GetCandidateDashboardAsync(User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/dashboard")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> GetHrDashboard()
    {
        var result = await _dashboardService.GetHrDashboardAsync();
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/manager/dashboard")]
    [Authorize(Roles = "Manager,HeadDepartment")]
    public async Task<IActionResult> GetManagerDashboard()
    {
        var result = await _dashboardService.GetManagerDashboardAsync();
        return StatusCode(result.StatusCode, result);
    }
}
