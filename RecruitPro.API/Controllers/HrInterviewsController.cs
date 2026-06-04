using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[Authorize(Roles = "HR,Manager,SystemAdmin")]
[Route("api/hr/interviews")]
[ApiController]
public class HrInterviewsController : ControllerBase
{
    private readonly IHrService _hrService;

    public HrInterviewsController(IHrService hrService)
    {
        _hrService = hrService;
    }

    [HttpGet]
    public async Task<IActionResult> GetInterviews([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] string? status = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var result = await _hrService.GetInterviewsAsync(page, pageSize, keyword, status, startDate, endDate);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("schedule-data")]
    public async Task<IActionResult> GetScheduleData()
    {
        var result = await _hrService.GetScheduleDataAsync();
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateInterview([FromBody] CreateInterviewRequest request)
    {
        var result = await _hrService.CreateInterviewAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("{interviewId}/status")]
    public async Task<IActionResult> UpdateInterviewStatus(string interviewId, [FromBody] UpdateInterviewStatusRequest request)
    {
        var result = await _hrService.UpdateInterviewStatusAsync(interviewId, request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{interviewId}")]
    public async Task<IActionResult> DeleteInterview(string interviewId)
    {
        var result = await _hrService.DeleteInterviewAsync(interviewId);
        return StatusCode(result.StatusCode, result);
    }
}
