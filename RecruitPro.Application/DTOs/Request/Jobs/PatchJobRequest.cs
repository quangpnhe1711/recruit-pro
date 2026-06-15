namespace RecruitPro.Application.DTOs.Request.Jobs;

public class PatchJobRequest
{
    public string? DepartmentId { get; set; }
    public string? Title { get; set; }
    public string? Department { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? Description { get; set; }
    public List<string>? Requirements { get; set; }
    public List<string>? Benefits { get; set; }
    public string? Location { get; set; }
    public string? WorkMode { get; set; }
    public string? EmploymentType { get; set; }
    public int? MinExperienceYears { get; set; }
    public int? VacancyCount { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public DateTime? Deadline { get; set; }
    public List<string>? SkillIds { get; set; }
    public List<string>? Skills { get; set; }
    public List<JobSkillRequirementRequest>? SkillRequirements { get; set; }
}
