using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request.Candidate;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
public class CandidateController : ControllerBase
{
    private readonly ICandidateService _candidateService;

    public CandidateController(ICandidateService candidateService)
    {
        _candidateService = candidateService;
    }

    [HttpPost("api/candidates/register")]
    [HttpPost("api/candidate/register")]
    public async Task<IActionResult> RegisterAsync([FromForm] CandidateRegisterRequest request, IFormFile? resume)
    {
        await using Stream? stream = resume?.OpenReadStream();
        string? fileName = null;
        string? contentType = null;

        if (resume != null)
        {
            fileName = resume.FileName;
            contentType = resume.ContentType;
        }

        var result = await _candidateService.RegisterAsync(request, stream, fileName, contentType);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/candidate/profile")]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _candidateService.GetProfileAsync(GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("api/candidate/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateCandidateProfileRequest request)
    {
        var result = await _candidateService.UpdateProfileAsync(GetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("api/candidate/profile/skills")]
    public async Task<IActionResult> UpdateSkills([FromBody] UpdateCandidateSkillsRequest request)
    {
        var result = await _candidateService.UpdateSkillsAsync(GetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/profile/experience")]
    public async Task<IActionResult> CreateExperience([FromBody] UpsertCandidateExperienceRequest request)
    {
        var result = await _candidateService.CreateExperienceAsync(GetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("api/candidate/profile/experience/{experienceId}")]
    public async Task<IActionResult> UpdateExperience(string experienceId, [FromBody] UpsertCandidateExperienceRequest request)
    {
        var result = await _candidateService.UpdateExperienceAsync(GetCurrentUserId(), experienceId, request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("api/candidate/profile/experience/{experienceId}")]
    public async Task<IActionResult> DeleteExperience(string experienceId)
    {
        var result = await _candidateService.DeleteExperienceAsync(GetCurrentUserId(), experienceId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/profile/resume")]
    public async Task<IActionResult> UploadResume(IFormFile resume)
    {
        await using Stream resumeStream = resume.OpenReadStream();
        var result = await _candidateService.UploadResumeAsync(GetCurrentUserId(), resumeStream, resume.FileName, resume.ContentType);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/candidates")]
    public async Task<IActionResult> GetCandidates([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] string? status = null, [FromQuery] string? source = null)
    {
        var result = await _candidateService.GetCandidatesAsync(page, pageSize, keyword, status, source);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/candidates/import/template")]
    public async Task<IActionResult> DownloadImportTemplate()
    {
        var result = await _candidateService.GenerateImportTemplateAsync();
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpPost("api/candidates/import/preview")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> PreviewImport(IFormFile file)
    {
        await using var fileStream = file.OpenReadStream();

        var result = await _candidateService.PreviewImportAsync(
            fileStream,
            file.FileName);

        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidates/import")]
    public async Task<IActionResult> ImportCandidates([FromBody] CandidateImportRequest request)
    {
        var result = await _candidateService.ImportCandidatesAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    private Guid GetCurrentUserId()
    {
        string sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("Missing user id claim.");
        return Guid.Parse(sub);
    }
}
