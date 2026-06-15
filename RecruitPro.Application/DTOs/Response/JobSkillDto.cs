using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecruitPro.Application.DTOs.Response
{
    public class JobSkillDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public decimal? MinYearsExperience { get; set; }

        public bool IsRequired { get; set; }

        public string SkillType { get; set; } = string.Empty;

        public decimal? MinimumYearsOfExperience { get; set; }
    }
}
