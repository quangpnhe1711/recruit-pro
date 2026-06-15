using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class Skill
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<JobSkill> JobSkills { get; set; } = new List<JobSkill>();

    public virtual ICollection<CandidateProfile> Candidates { get; set; } = new List<CandidateProfile>();

    public virtual ICollection<CandidateSkillDetail> CandidateSkillDetails { get; set; } = new List<CandidateSkillDetail>();
}
