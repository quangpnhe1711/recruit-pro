using Microsoft.AspNetCore.Mvc;
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

    [HttpGet("api/manager/reports/recruitment-analytics")]
    public async Task<IActionResult> GetRecruitmentAnalytics()
    {
        var result = await _managerAnalyticsService.GetRecruitmentAnalyticsAsync();
        return StatusCode(result.StatusCode, result);
    }
}
