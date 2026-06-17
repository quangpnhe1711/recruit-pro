using AutoMapper;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using System.Text.Json;

namespace RecruitPro.Application.Mappings;

public class SharedProfile : Profile
{
    public SharedProfile()
    {
        CreateMap<Department, DepartmentDto>().ConvertUsing(src => new DepartmentDto
        {
            Id = src.Id.ToString(),
            Name = src.Name,
            Description = src.Description
        });

        CreateMap<Skill, SkillLookupDto>().ConvertUsing(src => new SkillLookupDto
        {
            Id = src.Id.ToString(),
            Name = src.Name
        });

        CreateMap<JobSkill, JobSkillDto>().ConvertUsing(src => new JobSkillDto
        {
            Id = src.SkillId,
            Name = src.Skill.Name,
            MinYearsExperience = src.MinYearsExperience,
            IsRequired = src.IsRequired,
            SkillType = src.SkillType,
            MinimumYearsOfExperience = src.MinimumYearsOfExperience
        });

        CreateMap<User, HrJobCreatorDto>().ConvertUsing(src => new HrJobCreatorDto
        {
            Id = src.Id.ToString(),
            FullName = src.FullName,
            Email = src.Email
        });

        CreateMap<User, ManagerJobApprovalUserDto>().ConvertUsing(src => new ManagerJobApprovalUserDto
        {
            UserId = src.Id.ToString(),
            FullName = src.FullName,
            Email = src.Email,
            Phone = src.Phone
        });

        CreateMap<Department, ManagerJobApprovalDepartmentDto>().ConvertUsing(src => new ManagerJobApprovalDepartmentDto
        {
            DepartmentId = src.Id.ToString(),
            Name = src.Name,
            Description = src.Description
        });

        CreateMap<OfferTemplate, OfferTemplateOptionDto>().ConvertUsing(src => new OfferTemplateOptionDto
        {
            Id = src.Id.ToString(),
            Name = src.Name,
            Description = src.Description
        });

        CreateMap<OfferBenefit, OfferBenefitOptionDto>().ConvertUsing(src => new OfferBenefitOptionDto
        {
            Id = src.Id.ToString(),
            Name = src.Name,
            Description = src.Description
        });

        CreateMap<OfferCurrency, OfferCurrencyOptionDto>();

        CreateMap<User, OfferReportingManagerOptionDto>().ConvertUsing(src => new OfferReportingManagerOptionDto
        {
            Id = src.Id.ToString(),
            FullName = src.FullName,
            Email = src.Email,
            Title = src.UserRoles.Select(role => role.Role.Name).FirstOrDefault() ?? "Internal"
        });

        CreateMap<Interview, InterviewListItemDto>().ConvertUsing(src => new InterviewListItemDto
        {
            Id = src.Id.ToString(),
            CandidateName = src.Application.User.FullName,
            CandidateEmail = src.Application.User.Email,
            JobTitle = src.Application.Job.Title,
            Interviewer = "RecruitPro HR",
            DateLabel = src.InterviewDate.ToString("MMM dd, yyyy"),
            TimeLabel = $"{src.InterviewDate:HH:mm} - {src.InterviewDate.AddHours(1):HH:mm}",
            StartAt = src.InterviewDate,
            EndAt = src.InterviewDate.AddHours(1),
            Status = (src.Status ?? InterviewStatus.Scheduled).ToString()
        });

        CreateMap<CopilotConversation, CopilotConversationDto>().ConvertUsing(src => new CopilotConversationDto
        {
            ConversationId = src.Id,
            JobId = src.JobId,
            Title = src.Title ?? "AI Recruitment Copilot",
            LatestRankingSessionId = src.LatestRankingSessionId
        });

        CreateMap<CopilotMessage, CopilotMessageDto>().ConvertUsing(src => new CopilotMessageDto
        {
            MessageId = src.Id,
            Role = src.Role,
            Content = src.Content,
            MetadataJson = src.MetadataJson,
            SequenceNo = src.SequenceNo,
            CreatedAt = src.CreatedAt
        });

        CreateMap<CopilotConversation, CopilotConversationDetailDto>().ConvertUsing(src => new CopilotConversationDetailDto
        {
            ConversationId = src.Id,
            JobId = src.JobId,
            Title = src.Title ?? "AI Recruitment Copilot",
            LatestRankingSessionId = src.LatestRankingSessionId,
            Messages = src.Messages.OrderBy(message => message.SequenceNo).Select(message => new CopilotMessageDto
            {
                MessageId = message.Id,
                Role = message.Role,
                Content = message.Content,
                MetadataJson = message.MetadataJson,
                SequenceNo = message.SequenceNo,
                CreatedAt = message.CreatedAt
            }).ToList()
        });

        CreateMap<CopilotSavedRule, CopilotSavedRuleDto>().ConvertUsing(src => new CopilotSavedRuleDto
        {
            RuleId = src.Id,
            JobId = src.JobId,
            Name = src.Name,
            IsActive = src.IsActive,
            CreatedAt = src.CreatedAt,
            UpdatedAt = src.UpdatedAt,
            Rule = string.IsNullOrWhiteSpace(src.RuleJson)
                ? new CopilotNormalizedRulesDto()
                : JsonSerializer.Deserialize<CopilotNormalizedRulesDto>(src.RuleJson, new JsonSerializerOptions()) ?? new CopilotNormalizedRulesDto()
        });
    }
}
