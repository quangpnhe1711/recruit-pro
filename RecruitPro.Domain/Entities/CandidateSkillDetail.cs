using System;

namespace RecruitPro.Domain.Entities;

public partial class CandidateSkillDetail
{
    public Guid CandidateId { get; set; }

    public Guid SkillId { get; set; }

    public decimal? YearsOfExperience { get; set; }

    public virtual CandidateProfile Candidate { get; set; } = null!;

    public virtual Skill Skill { get; set; } = null!;
}
