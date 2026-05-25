using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class Department
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();
}
