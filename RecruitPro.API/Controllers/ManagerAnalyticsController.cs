using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
public class ManagerAnalyticsController : ControllerBase
{
    private readonly IManagerAnalyticsService _managerAnalyticsService;

    public ManagerAnalyticsController(IManagerAnalyticsService managerAnalyticsService)
    {
        _managerAnalyticsService = managerAnalyticsService;
    }

    // Phase 2.2b: SystemAdmin removed. Analytics is a business reporting surface restricted to
    // Manager and HeadDepartment roles.
    [HttpGet("api/manager/reports/recruitment-analytics")]
    [Authorize(Roles = "Manager,HeadDepartment")]
    public async Task<IActionResult> GetRecruitmentAnalytics()
    {
        var result = await _managerAnalyticsService.GetRecruitmentAnalyticsAsync();
        return StatusCode(result.StatusCode, result);
    }
}
