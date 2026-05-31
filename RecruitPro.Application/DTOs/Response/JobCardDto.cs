using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecruitPro.Application.DTOs.Response
{
    public sealed record JobCardDto
    {
        public Guid Id { get; init; }

        public string Icon { get; init; } = "data_object";

        public string Title { get; init; } = string.Empty;

        public string Meta { get; init; } = string.Empty;

        public string Salary { get; init; } = string.Empty;

        public string Posted { get; init; } = string.Empty;

        public List<string> Tags { get; init; } = [];

        public string Description { get; init; } = string.Empty;

        public string EmploymentType { get; init; } = string.Empty;

        public string WorkMode { get; init; } = string.Empty;
        public List<string> Skills { get; set; } = new List<string>();
    }
}
