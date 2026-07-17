using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RecruitPro.API.Extensions;
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

    // Phase 2.2b: SystemAdmin removed from all business interview endpoints.
    // HeadDepartment added to read-only endpoints (they hold Interview_VIEW permission in the DB and
    // INTERVIEW_VIEW_ALL in the frontend permission map). Write endpoints remain HR/Manager only.
    // Ownership scope (Interview -> Application -> Job) is enforced inside the service layer.
    [HttpGet("api/hr/interviews")]
    [Authorize(Roles = "HR,Manager,HeadDepartment")]
    public async Task<IActionResult> GetInterviews([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] string? status = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var result = await _interviewService.GetInterviewsAsync(page, pageSize, keyword, status, startDate, endDate, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/candidate/interviews")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> GetCandidateInterviews()
    {
        var result = await _interviewService.GetCandidateInterviewsAsync(User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/interviews/schedule-data")]
    [Authorize(Roles = "HR,Manager,HeadDepartment")]
    public async Task<IActionResult> GetScheduleData([FromQuery] string? applicationId = null)
    {
        var result = await _interviewService.GetScheduleDataAsync(applicationId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/hr/interviews")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> CreateInterview([FromBody] CreateInterviewRequest request)
    {
        var result = await _interviewService.CreateInterviewAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/hr/interviews/{interviewId}/status")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> UpdateInterviewStatus(string interviewId, [FromBody] UpdateInterviewStatusRequest request)
    {
        var result = await _interviewService.UpdateInterviewStatusAsync(interviewId, request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("api/hr/interviews/{interviewId}")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> DeleteInterview(string interviewId)
    {
        var result = await _interviewService.DeleteInterviewAsync(interviewId);
        return StatusCode(result.StatusCode, result);
    }

    // interview:confirm-own — the candidate acknowledges they will attend their Scheduled interview.
    // Ownership (interview -> application -> caller) is enforced in the service.
    [HttpPost("api/candidate/interviews/{interviewId}/confirm")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> ConfirmCandidateInterview(string interviewId)
    {
        var result = await _interviewService.ConfirmCandidateInterviewAsync(interviewId, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    // Post-interview scorecard (internal). Read is open to all interview readers; writing is an
    // HR/Manager review action and only valid once the interview is Completed.
    [HttpGet("api/hr/interviews/{interviewId}/evaluation")]
    [Authorize(Roles = "HR,Manager,HeadDepartment")]
    public async Task<IActionResult> GetInterviewEvaluation(string interviewId)
    {
        var result = await _interviewService.GetInterviewEvaluationAsync(interviewId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("api/hr/interviews/{interviewId}/evaluation")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> UpsertInterviewEvaluation(string interviewId, [FromBody] UpsertInterviewEvaluationRequest request)
    {
        var result = await _interviewService.UpsertInterviewEvaluationAsync(interviewId, User.TryGetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }
}
