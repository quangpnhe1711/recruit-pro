using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using RecruitPro.API.OData;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.API.Controllers.OData;

// OData entity set "Jobs" → /odata/Jobs. Public, read-only, Approved jobs only (mirrors the public job
// board). Supports $filter/$orderby/$select/$top/$skip/$count.
// Example: /odata/Jobs?$filter=contains(Title,'Java')&$orderby=CreatedAt desc&$top=5
[ApiExplorerSettings(IgnoreApi = true)] // keep Swashbuckle off the OData routes
public class JobsController : ODataController
{
    private readonly AppDbContext _db;
    public JobsController(AppDbContext db) => _db = db;

    [HttpGet]
    [EnableQuery]
    public IQueryable<JobODataDto> Get()
    {
        // ponytail: scalar columns pulled via SQL, then enum→string + OData query composed in-memory over
        // the bounded Approved set (demo scale). For large data, map enums differently and keep IQueryable.
        return _db.Jobs.AsNoTracking()
            .Where(j => j.Status == JobStatus.Approved)
            .Select(j => new
            {
                j.Id, j.Title, j.Location, j.WorkMode, j.EmploymentType, j.Status,
                j.MinExperienceYears, j.SalaryMin, j.SalaryMax, j.Deadline, j.CreatedAt
            })
            .AsEnumerable()
            .Select(j => new JobODataDto
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
            })
            .AsQueryable();
    }
}
