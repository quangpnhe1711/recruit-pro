using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using RecruitPro.API.Extensions;
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
    // ~6 MB = 5 MB CV cap (CandidateService) + multipart/form-field overhead. Framework rejects larger
    // bodies before model binding; the service still enforces the precise 5 MB on the file content.
    [RequestSizeLimit(6_291_456)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6_291_456)]
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
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _candidateService.GetProfileAsync(User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("api/candidate/profile")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateCandidateProfileRequest request)
    {
        var result = await _candidateService.UpdateProfileAsync(User.GetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/profile/save")]
    [Authorize(Roles = "Candidate")]
    [RequestSizeLimit(6_291_456)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6_291_456)]
    public async Task<IActionResult> SaveProfile([FromForm] string payload, IFormFile? resume)
    {
        UpdateCandidateProfileRequest? request = JsonSerializer.Deserialize<UpdateCandidateProfileRequest>(
            payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        if (request == null)
        {
            return BadRequest("Invalid profile payload.");
        }

        await using Stream? stream = resume?.OpenReadStream();
        var result = await _candidateService.SaveProfileAsync(
            User.GetCurrentUserId(),
            request,
            stream,
            resume?.FileName,
            resume?.ContentType);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("api/candidate/profile/skills")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> UpdateSkills([FromBody] UpdateCandidateSkillsRequest request)
    {
        var result = await _candidateService.UpdateSkillsAsync(User.GetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/profile/experience")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> CreateExperience([FromBody] UpsertCandidateExperienceRequest request)
    {
        var result = await _candidateService.CreateExperienceAsync(User.GetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("api/candidate/profile/experience/{experienceId}")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> UpdateExperience(string experienceId, [FromBody] UpsertCandidateExperienceRequest request)
    {
        var result = await _candidateService.UpdateExperienceAsync(User.GetCurrentUserId(), experienceId, request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("api/candidate/profile/experience/{experienceId}")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> DeleteExperience(string experienceId)
    {
        var result = await _candidateService.DeleteExperienceAsync(User.GetCurrentUserId(), experienceId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/profile/resume/parse")]
    [Authorize(Roles = "Candidate")]
    [RequestSizeLimit(6_291_456)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6_291_456)]
    public async Task<IActionResult> ParseResume(IFormFile resume)
    {
        await using Stream resumeStream = resume.OpenReadStream();
        var result = await _candidateService.ParseResumeAsync(User.GetCurrentUserId(), resumeStream, resume.FileName, resume.ContentType);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidate/profile/resume")]
    [Authorize(Roles = "Candidate")]
    [RequestSizeLimit(6_291_456)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6_291_456)]
    public async Task<IActionResult> UploadResume(IFormFile resume)
    {
        await using Stream resumeStream = resume.OpenReadStream();
        var result = await _candidateService.UploadResumeAsync(User.GetCurrentUserId(), resumeStream, resume.FileName, resume.ContentType);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/candidates")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> GetCandidates([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] string? status = null, [FromQuery] string? source = null)
    {
        var result = await _candidateService.GetCandidatesAsync(page, pageSize, keyword, status, source, User.TryGetCurrentUserId(), User.GetRoles());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/candidates/{candidateId}")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> GetCandidateDetail(string candidateId)
    {
        var result = await _candidateService.GetCandidateDetailAsync(candidateId, User.TryGetCurrentUserId(), User.GetRoles());
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
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> PreviewImport(IFormFile file)
    {
        await using var fileStream = file.OpenReadStream();

        var result = await _candidateService.PreviewImportAsync(
            fileStream,
            file.FileName);

        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/candidates/import")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> ImportCandidates([FromBody] CandidateImportRequest request)
    {
        var result = await _candidateService.ImportCandidatesAsync(request);
        return StatusCode(result.StatusCode, result);
    }
}
