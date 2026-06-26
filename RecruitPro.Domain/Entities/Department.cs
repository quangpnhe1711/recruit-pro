using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class Department
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    // Phase 1 ownership: the head of this Department — the default job approver and business reviewer
    // for the Department's applications (BR-OWN-001/003). Nullable during the migration window; the
    // effective department head falls back to Job.ApprovedBy when this is null.
    public Guid? HeadUserId { get; set; }

    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();

    public virtual User? HeadUser { get; set; }
}
