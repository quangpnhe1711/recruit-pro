using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RecruitPro.API.Extensions;
using RecruitPro.Application.DTOs.Request.Discovery;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
public class SemanticDiscoveryController : ControllerBase
{
    private readonly ISemanticDiscoveryService _semanticDiscoveryService;

    public SemanticDiscoveryController(ISemanticDiscoveryService semanticDiscoveryService)
    {
        _semanticDiscoveryService = semanticDiscoveryService;
    }

    // Phase 2.2b: SystemAdmin removed from all business discovery endpoints. These surfaces return
    // candidate PII and job-ranking data; access is restricted to HR/Manager business roles only.
    // The job-based recommended-candidates endpoint additionally enforces job ownership in the service.
    [HttpGet("api/hr/talent-pool/search")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> SearchTalentPool([FromQuery] TalentPoolSearchRequest request)
    {
        var result = await _semanticDiscoveryService.SearchTalentPoolAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/hr/candidate-discovery")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> DiscoverCandidates([FromBody] CandidateDiscoveryRequest request)
    {
        var result = await _semanticDiscoveryService.DiscoverCandidatesAsync(request);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/candidates/{candidateId}/similar")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> GetSimilarCandidates(string candidateId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _semanticDiscoveryService.GetSimilarCandidatesAsync(candidateId, page, pageSize);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/jobs/{jobId}/similar")]
    public async Task<IActionResult> GetSimilarJobs(string jobId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _semanticDiscoveryService.GetSimilarJobsAsync(jobId, page, pageSize);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/hr/jobs/{jobId}/recommended-candidates")]
    [Authorize(Roles = "HR,Manager")]
    public async Task<IActionResult> GetRecommendedCandidates(string jobId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _semanticDiscoveryService.GetRecommendedCandidatesAsync(jobId, User.TryGetCurrentUserId(), User.GetRoles(), page, pageSize);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/candidate/jobs/recommendations")]
    [Authorize(Roles = "Candidate")]
    public async Task<IActionResult> GetRecommendedJobs([FromQuery] int take = 5)
    {
        var result = await _semanticDiscoveryService.GetRecommendedJobsForCandidateAsync(User.GetCurrentUserId(), take);
        return StatusCode(result.StatusCode, result);
    }
}
