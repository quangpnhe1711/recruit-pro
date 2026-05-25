using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class Skill
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<CandidateProfile> Candidates { get; set; } = new List<CandidateProfile>();

    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();
}
