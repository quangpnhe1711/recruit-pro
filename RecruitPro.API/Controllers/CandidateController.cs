using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request.Candidate;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers
{
    [ApiController]
    public class CandidateController : ControllerBase
    {
        private readonly ICandidateProfileService _candidateService;

        public CandidateController(ICandidateProfileService candidateService)
        {
            _candidateService = candidateService;
        }

        [HttpPost("api/candidates/register")]
        [HttpPost("api/candidate/register")]
        public async Task<IActionResult> RegisterAsync([FromForm] CandidateRegisterRequest request, IFormFile? resume)
        {
            Stream? stream = null;
            string? fileName = null;
            string? contentType = null;

            if (resume != null)
            {
                stream = resume.OpenReadStream();
                fileName = resume.FileName;
                contentType = resume.ContentType;
            }

            var result = await _candidateService.RegisterAsync(request, stream, fileName, contentType);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Candidate")]
        [HttpGet("api/candidate/dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var result = await _candidateService.GetDashboardAsync(GetCurrentUserId());
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Candidate")]
        [HttpGet("api/candidate/applications")]
        public async Task<IActionResult> GetApplications([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? status = null, [FromQuery] string? keyword = null)
        {
            var result = await _candidateService.GetApplicationsAsync(GetCurrentUserId(), page, pageSize, status, keyword);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Candidate")]
        [HttpPost("api/candidate/applications/{applicationId}/withdraw")]
        public async Task<IActionResult> WithdrawApplication(string applicationId)
        {
            var result = await _candidateService.WithdrawApplicationAsync(GetCurrentUserId(), applicationId);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Candidate")]
        [HttpGet("api/candidate/profile")]
        public async Task<IActionResult> GetProfile()
        {
            var result = await _candidateService.GetProfileAsync(GetCurrentUserId());
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Candidate")]
        [HttpPut("api/candidate/profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateCandidateProfileRequest request)
        {
            var result = await _candidateService.UpdateProfileAsync(GetCurrentUserId(), request);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Candidate")]
        [HttpPut("api/candidate/profile/skills")]
        public async Task<IActionResult> UpdateSkills([FromBody] UpdateCandidateSkillsRequest request)
        {
            var result = await _candidateService.UpdateSkillsAsync(GetCurrentUserId(), request);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Candidate")]
        [HttpPost("api/candidate/profile/resume")]
        public async Task<IActionResult> UploadResume(IFormFile resume)
        {
            var result = await _candidateService.UploadResumeAsync(GetCurrentUserId(), resume.OpenReadStream(), resume.FileName, resume.ContentType);
            return StatusCode(result.StatusCode, result);
        }

        private Guid GetCurrentUserId()
        {
            var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? throw new UnauthorizedAccessException("Missing user id claim.");
            return Guid.Parse(sub);
        }
    }
}
