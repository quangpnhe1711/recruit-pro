using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using RecruitPro.Domain.Constants;

namespace RecruitPro.Domain.Entities;

public partial class JobSkill
{
    public Guid JobId { get; set; }

    public Guid SkillId { get; set; }

    public decimal? MinYearsExperience { get; set; }

    public bool IsRequired { get; set; }

    public virtual Job Job { get; set; } = null!;

    public virtual Skill Skill { get; set; } = null!;

    [NotMapped]
    public string SkillType
    {
        get => IsRequired ? JobSkillTypes.Required : JobSkillTypes.NiceToHave;
        set => IsRequired = !string.Equals(value, JobSkillTypes.NiceToHave, StringComparison.OrdinalIgnoreCase);
    }

    [NotMapped]
    public decimal? MinimumYearsOfExperience
    {
        get => MinYearsExperience;
        set => MinYearsExperience = value;
    }
}
