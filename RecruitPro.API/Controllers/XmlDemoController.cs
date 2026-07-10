using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecruitPro.API.OData;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.API.Controllers;

// Content-negotiation demo: the SAME endpoint returns JSON or XML depending on the Accept header.
// Returns a flat DTO (no ApiResponse envelope) so the XML serializer can handle the whole graph —
// the wrapped ApiResponse<T> graphs on other endpoints are not XML-serializable and stay JSON.
[ApiController]
[Route("api/xml-demo")]
[Produces("application/json", "application/xml")]
public class XmlDemoController : ControllerBase
{
    private readonly AppDbContext _db;
    public XmlDemoController(AppDbContext db) => _db = db;

    [HttpGet("jobs")]
    public async Task<ActionResult<List<JobODataDto>>> GetJobs()
    {
        var rows = await _db.Jobs.AsNoTracking()
            .Where(j => j.Status == JobStatus.Approved)
            .Select(j => new
            {
                j.Id, j.Title, j.Location, j.WorkMode, j.EmploymentType, j.Status,
                j.MinExperienceYears, j.SalaryMin, j.SalaryMax, j.Deadline, j.CreatedAt
            })
            .ToListAsync();

        var list = rows.Select(j => new JobODataDto
        {
            Id = j.Id,
            Title = j.Title,
            Location = j.Location,
            WorkMode = j.WorkMode.ToString(),
            EmploymentType = j.EmploymentType.ToString(),
            Status = j.Status.ToString(),
            MinExperienceYears = j.MinExperienceYears,
            SalaryMin = j.SalaryMin,
            SalaryMax = j.SalaryMax,
            Deadline = j.Deadline,
            CreatedAt = j.CreatedAt
        }).ToList();

        return Ok(list);
    }
}
