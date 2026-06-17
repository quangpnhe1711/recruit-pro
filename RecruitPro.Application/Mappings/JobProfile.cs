using System.Globalization;
using AutoMapper;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Mappings
{
    public class JobProfile : Profile
    {
        public JobProfile()
        {
            CreateMap<Job, JobCardDto>()
                .ForMember(dest => dest.Meta, opt => opt.MapFrom(src => BuildMeta(src)))
                .ForMember(dest => dest.Salary, opt => opt.MapFrom(src => FormatSalary(src.SalaryMin, src.SalaryMax)))
                .ForMember(dest => dest.Posted, opt => opt.MapFrom(src => FormatPosted(src.CreatedAt)))
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => BuildTags(src)))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description ?? string.Empty))
                .ForMember(dest => dest.EmploymentType, opt => opt.MapFrom(src => src.EmploymentType.ToString()))
                .ForMember(dest => dest.WorkMode, opt => opt.MapFrom(src => src.WorkMode.ToString()))
                .ForMember(dest => dest.Skills, opt => opt.MapFrom(src =>
                    src.JobSkills
                        .Select(jobSkill => jobSkill.Skill.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct()
                        .ToList()));
        }

        private static string BuildMeta(Job job)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(job.Department?.Name))
            {
                parts.Add(job.Department!.Name);
            }

            if (!string.IsNullOrWhiteSpace(job.Location))
            {
                parts.Add(job.Location);
            }

            return parts.Count > 0 ? string.Join(" • ", parts) : string.Empty;
        }

        private static List<string> BuildTags(Job job)
        {
            var tags = new List<string>();

            if (!string.IsNullOrWhiteSpace(job.Requirements))
            {
                tags.AddRange(
                    job.Requirements.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            if (tags.Count == 0)
            {
                tags.AddRange(
                    job.JobSkills
                        .Select(jobSkill => jobSkill.Skill.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct());
            }

            return tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string FormatSalary(decimal? minSalary, decimal? maxSalary)
        {
            if (minSalary is null && maxSalary is null)
            {
                return "Negotiable";
            }

            if (minSalary is not null && maxSalary is not null)
            {
                return $"{FormatAmount(minSalary.Value)} - {FormatAmount(maxSalary.Value)}";
            }

            return FormatAmount(minSalary ?? maxSalary!.Value);
        }

        private static string FormatAmount(decimal salary)
        {
            return salary.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatPosted(DateTime? createdAt)
        {
            return createdAt?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }
}
