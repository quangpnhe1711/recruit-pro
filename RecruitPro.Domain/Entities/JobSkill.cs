using System;
using System.Collections.Generic;

namespace RecruitPro.Domain.Entities;

public partial class JobSkill
{
    public Guid JobId { get; set; }

    public Guid SkillId { get; set; }

    public int? MinYearsExperience { get; set; }

    public bool IsRequired { get; set; }

    public virtual Job Job { get; set; } = null!;

    public virtual Skill Skill { get; set; } = null!;
}
