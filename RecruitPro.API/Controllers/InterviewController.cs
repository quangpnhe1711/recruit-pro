using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
public class InterviewController : ControllerBase
{
    private readonly IInterviewService _interviewService;

    public InterviewController(IInterviewService interviewService)
    {
        _interviewService = interviewService;
    }

    [HttpGet("api/hr/interviews")]
    public async Task<IActionResult> GetInterviews([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] string? status = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var result = await _interviewService.GetInterviewsAsync(page, pageSize, keyword, status, startDate, endDate);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/interviews/schedule-data")]
    public async Task<IActionResult> GetScheduleData([FromQuery] string? applicationId = null)
    {
        var result = await _interviewService.GetScheduleDataAsync(applicationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/hr/interviews")]
    public async Task<IActionResult> CreateInterview([FromBody] CreateInterviewRequest request)
    {
        var result = await _interviewService.CreateInterviewAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/hr/interviews/{interviewId}/status")]
    public async Task<IActionResult> UpdateInterviewStatus(string interviewId, [FromBody] UpdateInterviewStatusRequest request)
    {
        var result = await _interviewService.UpdateInterviewStatusAsync(interviewId, request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("api/hr/interviews/{interviewId}")]
    public async Task<IActionResult> DeleteInterview(string interviewId)
    {
        var result = await _interviewService.DeleteInterviewAsync(interviewId);
        return StatusCode(result.StatusCode, result);
    }
}
