using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecruitPro.Infrastructure.Data;
using RecruitPro.ScoringService.Grpc;

namespace RecruitPro.API.Controllers;

// Demonstrates service-to-service communication: the API gathers an application's skills/experience and
// delegates match scoring to the standalone RecruitPro.ScoringService over gRPC.
[ApiController]
[Authorize(Roles = "HR,Manager")]
public class ExternalScoreController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly Scorer.ScorerClient _scorer;

    public ExternalScoreController(AppDbContext db, Scorer.ScorerClient scorer)
    {
        _db = db;
        _scorer = scorer;
    }

    [HttpGet("api/hr/applications/{applicationId:guid}/external-score")]
    public async Task<IActionResult> GetExternalScore(Guid applicationId)
    {
        var app = await _db.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == applicationId);
        if (app is null) return NotFound(new { message = "Application not found" });

        var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == app.JobId);

        var requiredSkills = await _db.JobSkills.AsNoTracking()
            .Where(js => js.JobId == app.JobId && js.IsRequired)
            .Select(js => js.Skill.Name)
            .ToListAsync();

        var profile = await _db.CandidateProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == app.UserId);

        var candidateSkills = profile is null
            ? new List<string>()
            : await _db.CandidateSkills.AsNoTracking()
                .Where(cs => cs.CandidateId == profile.Id)
                .Select(cs => cs.Skill.Name)
                .ToListAsync();

        var request = new ScoreRequest
        {
            CandidateExperienceYears = profile?.ExperienceYears ?? 0,
            RequiredExperienceYears = job?.MinExperienceYears ?? 0
        };
        request.CandidateSkills.AddRange(candidateSkills);
        request.RequiredSkills.AddRange(requiredSkills);

        var reply = await _scorer.ScoreCandidateAsync(request);

        return Ok(new
        {
            applicationId,
            score = reply.Score,
            label = reply.Label,
            matchedSkills = reply.MatchedSkills,
            missingSkills = reply.MissingSkills,
            source = "RecruitPro.ScoringService (gRPC)"
        });
    }
}
