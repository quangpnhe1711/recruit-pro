using AutoMapper;
using RecruitPro.Application.DTOs.Request.Copilot;
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
            ApplicationId = src.ApplicationId.ToString(),
            CandidateName = src.Application.User.FullName,
            CandidateEmail = src.Application.User.Email,
            JobTitle = src.Application.Job.Title,
            Interviewer = src.Interviewer != null ? src.Interviewer.FullName : "RecruitPro HR",
            DateLabel = src.InterviewDate.ToString("MMM dd, yyyy"),
            TimeLabel = $"{src.InterviewDate:HH:mm} - {src.InterviewDate.AddMinutes(src.DurationMinutes > 0 ? src.DurationMinutes : 60):HH:mm}",
            StartAt = src.InterviewDate,
            EndAt = src.InterviewDate.AddMinutes(src.DurationMinutes > 0 ? src.DurationMinutes : 60),
            Status = (src.Status ?? InterviewStatus.Scheduled).ToString(),
            MeetingType = src.MeetingType.HasValue ? src.MeetingType.Value.ToString() : string.Empty,
            MeetingLink = src.MeetingLink,
            Location = src.Location,
            CandidateConfirmedAt = src.CandidateConfirmedAt
        });

        // Ranking with custom criteria maps the inbound request criteria onto the normalized rule DTO.
        // Both DTOs share the same fields (Label/Field/Operator/Value/Weight/AutoReject), so the default
        // by-name mapping is sufficient — its absence was the 500 ("Missing type map configuration").
        CreateMap<CopilotRuleCriterionRequestDto, CopilotRuleCriterionDto>();

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

        // Reload of a persisted ranking session (GET /api/copilot/ranking-sessions/{id}). Without these
        // maps AutoMapper threw "Missing type map configuration" -> 500 when the UI restored a job that
        // had already been ranked. The persisted columns are reversed here: NormalizedRulesJson,
        // Strengths/Weaknesses JSON arrays, and the {Summary, IsAiGenerated} ExplanationJson payload.
        CreateMap<CopilotRankingResult, CopilotRankingResultDto>().ConvertUsing(src => MapRankingResult(src));

        CreateMap<CopilotRankingSession, CopilotRankingSessionDetailDto>().ConvertUsing(src => new CopilotRankingSessionDetailDto
        {
            RankingSessionId = src.Id,
            ConversationId = src.ConversationId,
            JobId = src.JobId,
            UserPrompt = src.UserPrompt,
            ModelName = src.ModelName,
            TotalCandidates = src.TotalCandidates,
            PromptTokens = src.PromptTokens,
            CompletionTokens = src.CompletionTokens,
            CreatedAt = src.CreatedAt,
            NormalizedRules = DeserializeRules(src.NormalizedRulesJson),
            Results = src.Results
                .OrderBy(result => result.IsAutoRejected)
                .ThenBy(result => result.RankPosition)
                .Select(result => MapRankingResult(result))
                .ToList()
        });
    }

    private static CopilotNormalizedRulesDto DeserializeRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CopilotNormalizedRulesDto();
        }

        try
        {
            return JsonSerializer.Deserialize<CopilotNormalizedRulesDto>(json, JsonReadOptions)
                ?? new CopilotNormalizedRulesDto();
        }
        catch (JsonException)
        {
            return new CopilotNormalizedRulesDto();
        }
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonReadOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static CopilotRankingResultDto MapRankingResult(CopilotRankingResult src)
    {
        RankingExplanation explanation = DeserializeExplanation(src.ExplanationJson);
        return new CopilotRankingResultDto
        {
            CandidateUserId = src.CandidateUserId,
            ApplicationId = src.ApplicationId,
            FullName = src.Application?.User?.FullName ?? string.Empty,
            RankPosition = src.RankPosition,
            TotalScore = src.TotalScore,
            SkillScore = src.SkillScore,
            ExperienceScore = src.ExperienceScore,
            EducationScore = src.EducationScore,
            ProjectScore = src.ProjectScore,
            Recommendation = src.Recommendation,
            IsAutoRejected = src.IsAutoRejected,
            RejectReason = src.RejectReason,
            Strengths = DeserializeStringList(src.StrengthsJson),
            Weaknesses = DeserializeStringList(src.WeaknessesJson),
            FitLabel = explanation.FitLabel,
            ConfidenceScore = explanation.ConfidenceScore,
            Evidence = explanation.Evidence,
            Summary = explanation.Summary,
            IsAiGenerated = explanation.IsAiGenerated
        };
    }

    private static RankingExplanation DeserializeExplanation(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new RankingExplanation();
        }

        try
        {
            return JsonSerializer.Deserialize<RankingExplanation>(json, JsonReadOptions) ?? new RankingExplanation();
        }
        catch (JsonException)
        {
            return new RankingExplanation();
        }
    }

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class RankingExplanation
    {
        public string Summary { get; set; } = string.Empty;
        public bool IsAiGenerated { get; set; }
        // v2: fit-style evaluation persisted alongside the ranking result so a reload shows the same
        // fit label / confidence / Vietnamese evidence without re-running ranking.
        public string FitLabel { get; set; } = string.Empty;
        public decimal ConfidenceScore { get; set; }
        public List<string> Evidence { get; set; } = [];
    }
}
