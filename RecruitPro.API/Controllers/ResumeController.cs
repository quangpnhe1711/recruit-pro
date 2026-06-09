using Microsoft.AspNetCore.Mvc;
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

    /// <summary>
    /// Generates a temporary download URL for a private resume file.
    /// </summary>
    [HttpGet("api/resumes/{resumeId}/download")]
    public async Task<IActionResult> DownloadResume(string resumeId)
    {
        var result = await _candidateService.GetResumeDownloadUrlAsync(resumeId);
        return StatusCode(result.StatusCode, result);
    }
}
