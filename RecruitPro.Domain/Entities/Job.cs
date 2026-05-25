using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class Job
{
    public Guid Id { get; set; }

    public Guid? DepartmentId { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid? ApprovedBy { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? Requirements { get; set; }

    public string? Location { get; set; }

    public decimal? SalaryMin { get; set; }

    public decimal? SalaryMax { get; set; }

    public DateTime? Deadline { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Application> Applications { get; set; } = new List<Application>();

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Department? Department { get; set; }

    public virtual ICollection<Skill> Skills { get; set; } = new List<Skill>();
}
