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

        public List<JobSkillDto> Skills { get; set; } = [];

        public decimal? SalaryMin { get; set; }

        public decimal? SalaryMax { get; set; }

        public DateTime? Deadline { get; set; }

        public string JobType { get; set; }

        public string SalaryRange { get; set; }

        public string Posted { get; set; }

        public int? VacancyCount { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}
