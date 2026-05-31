using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecruitPro.Domain.Entities
{
    public class JobSkill
    {
        public Guid JobId { get; set; }

        public Guid SkillId { get; set; }

        public int? MinYearsExperience { get; set; }

        public bool IsRequired { get; set; }

        public Job Job { get; set; } = null!;

        public Skill Skill { get; set; } = null!;
    }
}
