using System.ComponentModel.DataAnnotations;

namespace RecruitPro.Application.DTOs.Request.Jobs;

public class CreateJobRequest
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public string? DepartmentId { get; set; }

    public string? Department { get; set; }

    // Phase 2/3: the recruiter who will own this job's applications (Job.RecruiterId). When omitted, the
    // creating HR user is used as a compatibility fallback (documented in JOB-APPROVAL-FLOW.md).
    public string? RecruiterId { get; set; }

    public string? EmploymentType { get; set; }

    public string? WorkMode { get; set; }

    public string? Location { get; set; }

    public string? ShortPitch { get; set; }

    public string? Description { get; set; }

    public List<string> Responsibilities { get; set; } = [];

    public List<string> Requirements { get; set; } = [];

    public List<string> Skills { get; set; } = [];

    public List<string> SkillIds { get; set; } = [];

    public List<JobSkillRequirementRequest> SkillRequirements { get; set; } = [];

    public decimal? SalaryMin { get; set; }

    public decimal? SalaryMax { get; set; }

    public string Currency { get; set; } = "VND";

    public int VacancyCount { get; set; } = 1;

    public int? MinExperienceYears { get; set; }

    public List<string> Benefits { get; set; } = [];

    public DateTime? Deadline { get; set; }
}

public class JobSkillRequirementRequest
{
    public string? SkillId { get; set; }
    public string? SkillName { get; set; }
    public string SkillType { get; set; } = "Required";
    public decimal? MinimumYearsOfExperience { get; set; }
}
