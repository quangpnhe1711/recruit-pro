using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RecruitPro.Application.DTOs.Response
{
    public class JobDetailResponseDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; }

        public List<string> Description { get; set; } = [];

        public string Department { get; set; }

        public string Location { get; set; }

        public string WorkMode { get; set; }

        public List<string> Requirements { get; set; } = [];

        public List<JobSkillDto> RequiredSkills { get; set; } = [];

        public List<JobSkillDto> NiceToHaveSkills { get; set; } = [];

        public List<JobSkillDto> Skills { get; set; } = [];

        public decimal? SalaryMin { get; set; }

        public decimal? SalaryMax { get; set; }

        public DateTime? Deadline { get; set; }

        public string JobType { get; set; }

        public string SalaryRange { get; set; }

        public string SalaryLabel { get; set; } = string.Empty;

        public string Posted { get; set; }

        public int? VacancyCount { get; set; }

        public string Status { get; set; } = string.Empty;

        // Phase 2/3 ownership. Recruiter = business owner; CreatedBy/ApprovedBy are audit fields only.
        // EffectiveDepartmentHead = Department.HeadUser ?? ApprovedBy user (BR-OWN-003).
        public string? RecruiterId { get; set; }
        public string? RecruiterName { get; set; }
        public string? RecruiterEmail { get; set; }

        public string? DepartmentHeadId { get; set; }
        public string? DepartmentHeadName { get; set; }
        public string? DepartmentHeadEmail { get; set; }

        public string? EffectiveDepartmentHeadId { get; set; }
        public string? EffectiveDepartmentHeadName { get; set; }
        public string? EffectiveDepartmentHeadEmail { get; set; }

        public string? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public string? ApprovedBy { get; set; }
        public string? ApprovedByName { get; set; }
    }
}
