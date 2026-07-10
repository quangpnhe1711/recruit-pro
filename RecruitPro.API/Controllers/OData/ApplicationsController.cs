using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using RecruitPro.API.OData;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.API.Controllers.OData;

// OData entity set "Applications" → /odata/Applications. Role-gated (contains recruitment data),
// read-only. Supports $filter/$orderby/$select/$top/$skip/$count.
// Example: /odata/Applications?$filter=Status eq 'Interview'&$select=Id,Status&$orderby=AppliedAt desc
[Authorize(Roles = "HR,Manager")]
[ApiExplorerSettings(IgnoreApi = true)]
public class ApplicationsController : ODataController
{
    private readonly AppDbContext _db;
    public ApplicationsController(AppDbContext db) => _db = db;

    [HttpGet]
    [EnableQuery]
    public IQueryable<ApplicationODataDto> Get()
    {
        // ponytail: scalar columns via SQL, then enum→string + OData query in-memory (demo scale).
        return _db.Applications.AsNoTracking()
            .Select(a => new
            {
                a.Id, a.JobId, a.UserId, a.Status, a.AppliedAt,
                a.RuleScore, a.SemanticScore, a.FinalScore, a.ScoreStatus
            })
            .AsEnumerable()
            .Select(a => new ApplicationODataDto
            {
                Id = a.Id,
                JobId = a.JobId,
                UserId = a.UserId,
                Status = a.Status.ToString(),
                AppliedAt = a.AppliedAt,
                RuleScore = a.RuleScore,
                SemanticScore = a.SemanticScore,
                FinalScore = a.FinalScore,
                ScoreStatus = a.ScoreStatus
            })
            .AsQueryable();
    }
}
