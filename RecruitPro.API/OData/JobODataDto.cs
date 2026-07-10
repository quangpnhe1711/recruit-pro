namespace RecruitPro.API.OData;

// Flat, read-only projection of a Job for OData querying. Enums are exposed as strings so filters read
// naturally (e.g. $filter=Status eq 'Approved'). Kept separate from the domain entity to avoid exposing
// navigation properties / embedding vectors over the query surface.
public class JobODataDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string WorkMode { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? MinExperienceYears { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime? CreatedAt { get; set; }
}
