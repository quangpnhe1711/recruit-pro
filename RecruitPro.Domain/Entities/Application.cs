using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class Application
{
    public Guid Id { get; set; }

    public Guid CandidateId { get; set; }

    public Guid JobId { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? AppliedAt { get; set; }

    public virtual CandidateProfile Candidate { get; set; } = null!;

    public virtual ICollection<Interview> Interviews { get; set; } = new List<Interview>();

    public virtual Job Job { get; set; } = null!;

    public virtual User? ReviewedByNavigation { get; set; }
}
