using System;
using System.Collections.Generic;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Domain.Entities;

public partial class Job
{
    public Guid Id { get; set; }

    public Guid? DepartmentId { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid? ApprovedBy { get; set; }

    // Phase 1 ownership: the recruiter who owns this job's applications (BR-OWN-002). Nullable during
    // the migration window; falls back to CreatedBy. CreatedBy remains an audit field, not the owner.
    public Guid? RecruiterId { get; set; }

    public string Title { get; set; } = null!;

    public string? ShortPitch { get; set; }

    public string Description { get; set; } = null!;

    public string? Requirements { get; set; }

    public string? Benefits { get; set; }

    public string Location { get; set; } = null!;

    public WorkMode WorkMode { get; set; }

    public EmploymentType EmploymentType { get; set; }

    public int? MinExperienceYears { get; set; }

    public int? VacancyCount { get; set; }

    public decimal? SalaryMin { get; set; }

    public decimal? SalaryMax { get; set; }

    public DateTime? Deadline { get; set; }

    public JobStatus Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? JobEmbeddingVectorJson { get; set; }

    public string? JobEmbeddingTextHash { get; set; }

    public string? JobEmbeddingStatus { get; set; }

    public string? JobEmbeddingError { get; set; }

    public DateTime? JobEmbeddingUpdatedAt { get; set; }

    public virtual ICollection<Application> Applications { get; set; } = new List<Application>();

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual User? Recruiter { get; set; }

    public virtual Department? Department { get; set; }

    public virtual ICollection<JobSkill> JobSkills { get; set; } = new List<JobSkill>();
}
