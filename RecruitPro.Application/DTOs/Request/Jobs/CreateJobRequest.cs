using System.ComponentModel.DataAnnotations;

namespace RecruitPro.Application.DTOs.Request.Jobs;

public class CreateJobRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public string? DepartmentId { get; set; }

    public string? Department { get; set; }

    public string? EmploymentType { get; set; }

    public string? WorkMode { get; set; }

    public string? Location { get; set; }

    public string? ShortPitch { get; set; }

    public string? Description { get; set; }

    public List<string> Responsibilities { get; set; } = [];

    public List<string> Requirements { get; set; } = [];

    public List<string> Skills { get; set; } = [];

    public List<string> SkillIds { get; set; } = [];

    public decimal? SalaryMin { get; set; }

    public decimal? SalaryMax { get; set; }

    public string Currency { get; set; } = "USD";

    public int VacancyCount { get; set; } = 1;

    public int? MinExperienceYears { get; set; }

    public List<string> Benefits { get; set; } = [];

    public DateTime? Deadline { get; set; }
}
