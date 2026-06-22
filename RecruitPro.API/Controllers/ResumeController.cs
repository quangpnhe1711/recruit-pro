using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.API.Extensions;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
public class ResumeController : ControllerBase
{
    private readonly ICandidateService _candidateService;

    public ResumeController(ICandidateService candidateService)
    {
        _candidateService = candidateService;
    }

    [Authorize(Roles = "Candidate,HR,Manager")]
    [HttpGet("api/resumes/{resumeId}/preview")]
    public async Task<IActionResult> PreviewResume(string resumeId)
    {
        var result = await _candidateService.GetResumeStreamAsync(
            resumeId,
            User.GetCurrentUserId(),
            User.IsInRole("HR") || User.IsInRole("Manager"));
        if (result == null)
        {
            return NotFound(new { message = "Resume not found." });
        }

        Response.Headers.ContentDisposition = $"inline; filename=\"{result.FileName}\"";
        return File(result.Content, result.ContentType);
    }

    /// <summary>
    /// Streams a private resume file as an attachment.
    /// </summary>
    [Authorize(Roles = "Candidate,HR,Manager")]
    [HttpGet("api/resumes/{resumeId}/download")]
    public async Task<IActionResult> DownloadResume(string resumeId)
    {
        var result = await _candidateService.GetResumeStreamAsync(
            resumeId,
            User.GetCurrentUserId(),
            User.IsInRole("HR") || User.IsInRole("Manager"));
        if (result == null)
        {
            return NotFound(new { message = "Resume not found." });
        }

        return File(result.Content, result.ContentType, result.FileName);
    }
}
