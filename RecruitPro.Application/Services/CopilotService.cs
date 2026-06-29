using System.Text.Json;
using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Common;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.DTOs.Request.Copilot;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Services;

public class CopilotService : ICopilotService
{
    private readonly ICopilotRepository _copilotRepository;
    private readonly IJobRepository _jobRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IResumeTextExtractor _resumeTextExtractor;
    private readonly IAiCopilotProvider _aiCopilotProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AiProviderSettings _aiProviderSettings;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the CopilotService class.
    /// </summary>
    /// <param name="copilotRepository">The <paramref name="copilotRepository"/> value.</param>
    /// <param name="jobRepository">The <paramref name="jobRepository"/> value.</param>
    /// <param name="fileStorageService">The <paramref name="fileStorageService"/> value.</param>
    /// <param name="resumeTextExtractor">The <paramref name="resumeTextExtractor"/> value.</param>
    /// <param name="aiCopilotProvider">The <paramref name="aiCopilotProvider"/> value.</param>
    /// <param name="unitOfWork">The <paramref name="unitOfWork"/> value.</param>
    /// <param name="aiProviderOptions">The <paramref name="aiProviderOptions"/> value.</param>
    public CopilotService(
        ICopilotRepository copilotRepository,
        IJobRepository jobRepository,
        IFileStorageService fileStorageService,
        IResumeTextExtractor resumeTextExtractor,
        IAiCopilotProvider aiCopilotProvider,
        IUnitOfWork unitOfWork,
        IOptions<AiProviderSettings> aiProviderOptions,
        IMapper mapper)
    {
        _copilotRepository = copilotRepository;
        _jobRepository = jobRepository;
        _fileStorageService = fileStorageService;
        _resumeTextExtractor = resumeTextExtractor;
        _aiCopilotProvider = aiCopilotProvider;
        _unitOfWork = unitOfWork;
        _aiProviderSettings = aiProviderOptions.Value;
        _mapper = mapper;
    }

    /// <summary>
    /// Retrieves jobs.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<IReadOnlyList<CopilotJobOptionDto>>> GetJobsAsync()
    {
        IReadOnlyList<CopilotJobOptionDto> jobs = await _copilotRepository.GetJobOptionsAsync();
        return ApiResponse<IReadOnlyList<CopilotJobOptionDto>>.Ok(jobs);
    }

    /// <summary>
    /// Creates conversation.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CopilotConversationDto>> CreateConversationAsync(CreateCopilotConversationRequest request, Guid userId)
    {
        CopilotConversation? existing = await _copilotRepository.GetLatestConversationAsync(request.JobId, userId);
        if (existing is not null)
        {
            return ApiResponse<CopilotConversationDto>.Ok(_mapper.Map<CopilotConversationDto>(existing));
        }

        CopilotConversation conversation = new()
        {
            JobId = request.JobId,
            UserId = userId,
            Title = "AI Recruitment Copilot",
            Status = "Active"
        };

        await _copilotRepository.AddConversationAsync(conversation);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<CopilotConversationDto>.Created(_mapper.Map<CopilotConversationDto>(conversation));
    }

    /// <summary>
    /// Retrieves conversation.
    /// </summary>
    /// <param name="conversationId">The <paramref name="conversationId"/> value.</param>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CopilotConversationDetailDto>> GetConversationAsync(Guid conversationId, Guid userId)
    {
        CopilotConversation? conversation = await _copilotRepository.GetConversationWithDetailsAsync(conversationId);
        if (conversation is null || conversation.UserId != userId)
        {
            return ApiResponse<CopilotConversationDetailDto>.NotFound("Conversation not found");
        }

        return ApiResponse<CopilotConversationDetailDto>.Ok(_mapper.Map<CopilotConversationDetailDto>(conversation));
    }

    /// <summary>
    /// Retrieves candidate pool.
    /// </summary>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CopilotCandidatePoolDto>> GetCandidatePoolAsync(Guid jobId, Guid? callerUserId, IReadOnlyCollection<string> callerRoles)
    {
        Job? job = await _jobRepository.GetByIdAsync(jobId);
        if (job is null)
            return ApiResponse<CopilotCandidatePoolDto>.NotFound("Job not found");

        if (!OwnershipScope.CanAccessJob(job, callerUserId, callerRoles))
            return ApiResponse<CopilotCandidatePoolDto>.Forbidden("Bạn không có quyền xem candidate pool của job này.");

        CopilotCandidatePoolDto? pool = await _copilotRepository.GetCandidatePoolAsync(jobId);
        return pool is null
            ? ApiResponse<CopilotCandidatePoolDto>.NotFound("Job not found")
            : ApiResponse<CopilotCandidatePoolDto>.Ok(pool);
    }

    /// <summary>
    /// Creates ranking.
    /// </summary>
    /// <param name="conversationId">The <paramref name="conversationId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CopilotPromptResponseDto>> CreateRankingAsync(Guid conversationId, CopilotPromptRequest request, Guid userId)
    {
        CopilotConversation? conversation = await _copilotRepository.GetConversationAsync(conversationId);
        if (conversation is null || conversation.UserId != userId || conversation.JobId != request.JobId)
        {
            return ApiResponse<CopilotPromptResponseDto>.NotFound("Conversation not found");
        }

        CopilotCandidatePoolDto? pool = await _copilotRepository.GetCandidatePoolAsync(request.JobId);
        if (pool is null)
        {
            return ApiResponse<CopilotPromptResponseDto>.NotFound("Job not found");
        }

        bool shouldRunRanking = request.ForceRanking
            && (!string.IsNullOrWhiteSpace(request.Prompt)
                || request.PriorityCriteria.Count > 0
                || request.NegativeCriteria.Count > 0);

        if (!shouldRunRanking)
        {
            pool = await EnrichPoolWithResumeTextAsync(pool);
            string assistantReply = await _aiCopilotProvider.TryCreateChatReplyAsync(pool, request.Prompt, conversationId)
                ?? "AI copilot returned an empty response.";

            int replySequence = await _copilotRepository.GetNextMessageSequenceAsync(conversationId);
            await _copilotRepository.AddMessageAsync(new CopilotMessage
            {
                ConversationId = conversationId,
                Role = "User",
                Content = request.Prompt,
                SequenceNo = replySequence
            });
            await _copilotRepository.AddMessageAsync(new CopilotMessage
            {
                ConversationId = conversationId,
                Role = "Assistant",
                Content = assistantReply,
                SequenceNo = replySequence + 1
            });

            conversation.UpdatedAt = DbDateTime.Now;
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<CopilotPromptResponseDto>.Ok(new CopilotPromptResponseDto
            {
                ConversationId = conversationId,
                DidRank = false,
                AssistantMessage = assistantReply,
                NormalizedRules = new CopilotNormalizedRulesDto(),
                Results = []
            });
        }

        IReadOnlyList<CopilotSavedRule> savedRules = await _copilotRepository.GetSavedRulesAsync(request.JobId, userId);
        CopilotNormalizedRulesDto rules = BuildRules(
            request.Prompt,
            pool.Job.RequiredSkills,
            request.PriorityCriteria,
            request.NegativeCriteria,
            savedRules.Where(rule => rule.IsActive).ToList());

        if (request.UseLatestRankingContext && conversation.LatestRankingSessionId.HasValue)
        {
            CopilotRankingSession? latestSession = await _copilotRepository.GetRankingSessionAsync(conversation.LatestRankingSessionId.Value);
            if (latestSession is not null)
            {
                CopilotNormalizedRulesDto latestRules = DeserializeRules(latestSession.NormalizedRulesJson);
                rules = MergeRules(latestRules, rules);
            }
        }

        List<CopilotRankingResultDto> results = RankCandidates(pool.Candidates, rules);
        CopilotPromptResponseDto? aiResponse = await _aiCopilotProvider.TryCreateRankingAsync(
            pool,
            rules,
            results,
            request.Prompt,
            conversationId);

        if (aiResponse is not null && aiResponse.Results.Count > 0)
        {
            rules = aiResponse.NormalizedRules;
            results = NormalizeRankingResults(aiResponse.Results, pool.Candidates)
                .Select(result =>
                {
                    result.IsAiGenerated = !string.IsNullOrWhiteSpace(result.Summary);
                    return result;
                })
                .OrderBy(result => result.IsAutoRejected)
                .ThenBy(result => result.RankPosition)
                .ThenByDescending(result => result.TotalScore)
                .ToList();

            for (int i = 0; i < results.Count; i += 1)
            {
                results[i].RankPosition = i + 1;
            }
        }

        CopilotRankingSession session = new()
        {
            JobId = request.JobId,
            ConversationId = conversationId,
            UserId = userId,
            UserPrompt = request.Prompt,
            NormalizedRulesJson = JsonSerializer.Serialize(rules),
            TotalCandidates = pool.Candidates.Count,
            ModelName = results.Any(result => result.IsAiGenerated)
                ? _aiProviderSettings.Model
                : "deterministic-copilot-v1",
            Results = NormalizeRankingResults(results, pool.Candidates).Select(result => new CopilotRankingResult
            {
                CandidateUserId = result.CandidateUserId,
                ApplicationId = result.ApplicationId,
                RankPosition = result.RankPosition,
                TotalScore = result.TotalScore,
                SkillScore = result.SkillScore,
                ExperienceScore = result.ExperienceScore,
                EducationScore = result.EducationScore,
                ProjectScore = result.ProjectScore,
                Recommendation = result.Recommendation,
                RejectReason = result.RejectReason,
                IsAutoRejected = result.IsAutoRejected,
                StrengthsJson = JsonSerializer.Serialize(result.Strengths),
                WeaknessesJson = JsonSerializer.Serialize(result.Weaknesses),
                ExplanationJson = JsonSerializer.Serialize(new { result.Summary, result.IsAiGenerated })
            }).ToList()
        };

        int nextSequence = await _copilotRepository.GetNextMessageSequenceAsync(conversationId);
        await _copilotRepository.AddMessageAsync(new CopilotMessage
        {
            ConversationId = conversationId,
            Role = "User",
            Content = request.Prompt,
            SequenceNo = nextSequence
        });
        await _copilotRepository.AddMessageAsync(new CopilotMessage
        {
            ConversationId = conversationId,
            Role = "Assistant",
            Content = $"Ranked {results.Count} candidates. {results.Count(result => result.IsAutoRejected)} auto rejected.",
            MetadataJson = JsonSerializer.Serialize(new { rules, resultCount = results.Count }),
            SequenceNo = nextSequence + 1
        });

        await _copilotRepository.AddRankingSessionAsync(session);
        await _unitOfWork.SaveChangesAsync();

        conversation.LatestRankingSessionId = session.Id;
        conversation.UpdatedAt = DbDateTime.Now;
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<CopilotPromptResponseDto>.Ok(new CopilotPromptResponseDto
        {
            ConversationId = conversationId,
            RankingSessionId = session.Id,
            DidRank = true,
            AssistantMessage = BuildRankingAssistantMessage(results),
            NormalizedRules = rules,
            Results = results
        });
    }

    /// <summary>
    /// Retrieves ranking session.
    /// </summary>
    /// <param name="rankingSessionId">The <paramref name="rankingSessionId"/> value.</param>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CopilotRankingSessionDetailDto>> GetRankingSessionAsync(Guid rankingSessionId, Guid userId)
    {
        CopilotRankingSession? session = await _copilotRepository.GetRankingSessionAsync(rankingSessionId);
        if (session is null || session.UserId != userId)
        {
            return ApiResponse<CopilotRankingSessionDetailDto>.NotFound("Ranking session not found");
        }

        return ApiResponse<CopilotRankingSessionDetailDto>.Ok(_mapper.Map<CopilotRankingSessionDetailDto>(session));
    }

    /// <summary>
    /// Retrieves saved rules.
    /// </summary>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<IReadOnlyList<CopilotSavedRuleDto>>> GetSavedRulesAsync(Guid jobId, Guid userId)
    {
        IReadOnlyList<CopilotSavedRule> savedRules = await _copilotRepository.GetSavedRulesAsync(jobId, userId);
        return ApiResponse<IReadOnlyList<CopilotSavedRuleDto>>.Ok(_mapper.Map<List<CopilotSavedRuleDto>>(savedRules));
    }

    /// <summary>
    /// Creates saved rule.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CopilotSavedRuleDto>> CreateSavedRuleAsync(CreateCopilotSavedRuleRequest request, Guid userId)
    {
        CopilotNormalizedRulesDto normalizedRule = BuildRules(
            string.Empty,
            [],
            request.PriorityCriteria,
            request.NegativeCriteria,
            []);

        CopilotSavedRule rule = new()
        {
            JobId = request.JobId,
            UserId = userId,
            Name = ResolveSavedRuleName(request.Name, normalizedRule),
            RuleJson = JsonSerializer.Serialize(normalizedRule),
            IsActive = request.IsActive,
            UpdatedAt = DbDateTime.Now
        };

        await _copilotRepository.AddSavedRuleAsync(rule);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<CopilotSavedRuleDto>.Created(_mapper.Map<CopilotSavedRuleDto>(rule));
    }

    /// <summary>
    /// Updates saved rule status.
    /// </summary>
    /// <param name="ruleId">The <paramref name="ruleId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CopilotSavedRuleDto>> UpdateSavedRuleStatusAsync(Guid ruleId, UpdateCopilotSavedRuleStatusRequest request, Guid userId)
    {
        CopilotSavedRule? rule = await _copilotRepository.GetSavedRuleAsync(ruleId, userId);
        if (rule is null)
        {
            return ApiResponse<CopilotSavedRuleDto>.NotFound("Saved rule not found");
        }

        rule.IsActive = request.IsActive;
        rule.UpdatedAt = DbDateTime.Now;
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<CopilotSavedRuleDto>.Ok(_mapper.Map<CopilotSavedRuleDto>(rule));
    }

    /// <summary>
    /// Deletes saved rule.
    /// </summary>
    /// <param name="ruleId">The <paramref name="ruleId"/> value.</param>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<object>> DeleteSavedRuleAsync(Guid ruleId, Guid userId)
    {
        CopilotSavedRule? rule = await _copilotRepository.GetSavedRuleAsync(ruleId, userId);
        if (rule is null)
        {
            return ApiResponse<object>.NotFound("Saved rule not found");
        }

        rule.IsDeleted = true;
        rule.IsActive = false;
        rule.DeletedAt = DbDateTime.Now;
        rule.UpdatedAt = DbDateTime.Now;
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<object>.Ok(new { ruleId });
    }

    /// <summary>
    /// Executes the enrich pool with resume text operation.
    /// </summary>
    /// <param name="pool">The <paramref name="pool"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private async Task<CopilotCandidatePoolDto> EnrichPoolWithResumeTextAsync(CopilotCandidatePoolDto pool)
    {
        int maxChars = _aiProviderSettings.MaxResumeCharsPerCandidate > 0
            ? _aiProviderSettings.MaxResumeCharsPerCandidate
            : 6000;

        List<CopilotCandidateDto> enrichedCandidates = [];

        foreach (CopilotCandidateDto candidate in pool.Candidates)
        {
            string cvSummary = candidate.CvSummary;
            if (!string.IsNullOrWhiteSpace(candidate.ResumeUrl))
            {
                try
                {
                    await using Stream stream = await _fileStorageService.DownloadFileAsync(candidate.ResumeUrl);
                    string extractedText = await _resumeTextExtractor.ExtractTextAsync(stream);
                    if (!string.IsNullOrWhiteSpace(extractedText))
                    {
                        string clippedText = extractedText.Length > maxChars
                            ? extractedText[..maxChars]
                            : extractedText;
                        cvSummary = string.IsNullOrWhiteSpace(cvSummary)
                            ? clippedText
                            : $"{cvSummary}\n\nCV PDF Text:\n{clippedText}";
                    }
                }
                catch
                {
                    // Resume text is best-effort; ranking can still use profile data.
                }
            }

            enrichedCandidates.Add(new CopilotCandidateDto
            {
                CandidateUserId = candidate.CandidateUserId,
                ApplicationId = candidate.ApplicationId,
                FullName = candidate.FullName,
                Education = candidate.Education,
                ExperienceYears = candidate.ExperienceYears,
                Skills = candidate.Skills,
                CvSummary = cvSummary,
                ResumeUrl = candidate.ResumeUrl
            });
        }

        return new CopilotCandidatePoolDto
        {
            Job = pool.Job,
            Candidates = enrichedCandidates
        };
    }

    /// <summary>
    /// Resolves saved rule name.
    /// </summary>
    /// <param name="requestedName">The <paramref name="requestedName"/> value.</param>
    /// <param name="rule">The <paramref name="rule"/> value.</param>
    /// <returns>The resulting string value.</returns>
    private static string ResolveSavedRuleName(string? requestedName, CopilotNormalizedRulesDto rule)
    {
        if (!string.IsNullOrWhiteSpace(requestedName))
        {
            return requestedName.Trim();
        }

        List<string> labels = rule.PriorityCriteria
            .Select(criterion => string.IsNullOrWhiteSpace(criterion.Label) ? criterion.Value : criterion.Label)
            .Concat(rule.NegativeCriteria.Select(criterion => string.IsNullOrWhiteSpace(criterion.Label) ? criterion.Value : criterion.Label))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (labels.Count == 0)
        {
            return "Saved criteria";
        }

        string firstLabel = labels[0].Trim();
        return labels.Count == 1
            ? firstLabel
            : $"{firstLabel} +{labels.Count - 1}";
    }

    /// <summary>
    /// Builds rules.
    /// </summary>
    /// <param name="prompt">The <paramref name="prompt"/> value.</param>
    /// <param name="jobRequiredSkills">The <paramref name="jobRequiredSkills"/> value.</param>
    /// <param name="priorityCriteria">The <paramref name="priorityCriteria"/> value.</param>
    /// <param name="negativeCriteria">The <paramref name="negativeCriteria"/> value.</param>
    /// <param name="savedRules">The <paramref name="savedRules"/> value.</param>
    /// <returns>The operation result.</returns>
    private CopilotNormalizedRulesDto BuildRules(
        string prompt,
        IReadOnlyList<string> jobRequiredSkills,
        IReadOnlyList<CopilotRuleCriterionRequestDto> priorityCriteria,
        IReadOnlyList<CopilotRuleCriterionRequestDto> negativeCriteria,
        IReadOnlyList<CopilotSavedRule> savedRules)
    {
        string loweredPrompt = prompt.ToLowerInvariant();
        List<string> requiredSkills = jobRequiredSkills
            .Where(skill => loweredPrompt.Contains(skill.ToLowerInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<string> preferredSkills = [];

        string[] commonSkills = ["Java", "Spring Boot", "ReactJS", "Docker", "PostgreSQL", "SQL", "C#", ".NET", "AWS", "Redis", "NodeJS", "TypeScript"];
        requiredSkills.AddRange(commonSkills.Where(skill => loweredPrompt.Contains(skill.ToLowerInvariant())));
        requiredSkills = requiredSkills.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        List<CopilotAutoRejectRuleDto> autoRejectRules = [];
        if (loweredPrompt.Contains("fpt") && (loweredPrompt.Contains("loai") || loweredPrompt.Contains("loại") || loweredPrompt.Contains("reject")))
        {
            autoRejectRules.Add(new CopilotAutoRejectRuleDto
            {
                Field = "education",
                Operator = "contains",
                Value = "FPT",
                Reason = "FPT Student"
            });
        }

        int? minExperienceYears = null;
        Match yearMatch = Regex.Match(loweredPrompt, @"(?:tren|trên|>=|hon|hơn|over)\s*(\d+)\s*(?:nam|năm|years?)");
        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out int years))
        {
            minExperienceYears = years;
        }

        List<CopilotRuleCriterionDto> normalizedPriorityCriteria = priorityCriteria
            .Where(criteria => !string.IsNullOrWhiteSpace(criteria.Value))
            .Select(criterion => _mapper.Map<CopilotRuleCriterionDto>(criterion))
            .ToList();
        List<CopilotRuleCriterionDto> normalizedNegativeCriteria = negativeCriteria
            .Where(criteria => !string.IsNullOrWhiteSpace(criteria.Value))
            .Select(criterion => _mapper.Map<CopilotRuleCriterionDto>(criterion))
            .ToList();

        foreach (CopilotRuleCriterionDto criterion in normalizedPriorityCriteria)
        {
            if (criterion.Field.Equals("skill", StringComparison.OrdinalIgnoreCase))
            {
                if (criterion.Weight.Equals("high", StringComparison.OrdinalIgnoreCase))
                {
                    requiredSkills.Add(criterion.Value);
                }
                else
                {
                    preferredSkills.Add(criterion.Value);
                }
            }

            if (criterion.Field.Equals("experienceYears", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(criterion.Value, out int criterionYears))
            {
                minExperienceYears = Math.Max(minExperienceYears ?? 0, criterionYears);
            }
        }

        foreach (CopilotRuleCriterionDto criterion in normalizedNegativeCriteria)
        {
            if (criterion.AutoReject)
            {
                autoRejectRules.Add(new CopilotAutoRejectRuleDto
                {
                    Field = criterion.Field,
                    Operator = criterion.Operator,
                    Value = criterion.Value,
                    Reason = string.IsNullOrWhiteSpace(criterion.Label) ? criterion.Value : criterion.Label
                });
            }
        }

        foreach (CopilotSavedRule savedRule in savedRules)
        {
            CopilotNormalizedRulesDto savedRulesDto = DeserializeRules(savedRule.RuleJson);
            requiredSkills.AddRange(savedRulesDto.RequiredSkills);
            preferredSkills.AddRange(savedRulesDto.PreferredSkills);
            normalizedPriorityCriteria.AddRange(savedRulesDto.PriorityCriteria);
            normalizedNegativeCriteria.AddRange(savedRulesDto.NegativeCriteria);
            autoRejectRules.AddRange(savedRulesDto.AutoRejectRules);

            if (savedRulesDto.MinExperienceYears.HasValue)
            {
                minExperienceYears = Math.Max(minExperienceYears ?? 0, savedRulesDto.MinExperienceYears.Value);
            }
        }

        return new CopilotNormalizedRulesDto
        {
            RequiredSkills = requiredSkills.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            PreferredSkills = preferredSkills.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            MinExperienceYears = minExperienceYears,
            AutoRejectRules = autoRejectRules
                .GroupBy(rule => $"{rule.Field}|{rule.Operator}|{rule.Value}|{rule.Reason}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList(),
            PriorityCriteria = DeduplicateCriteria(normalizedPriorityCriteria),
            NegativeCriteria = DeduplicateCriteria(normalizedNegativeCriteria)
        };
    }

    /// <summary>
    /// Ranks candidates.
    /// </summary>
    /// <param name="candidates">The <paramref name="candidates"/> value.</param>
    /// <param name="rules">The <paramref name="rules"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<CopilotRankingResultDto> RankCandidates(IReadOnlyList<CopilotCandidateDto> candidates, CopilotNormalizedRulesDto rules)
    {
        List<CopilotRankingResultDto> results = candidates.Select(candidate =>
        {
            List<string> candidateSkills = candidate.Skills.ToList();
            string cvText = candidate.CvSummary ?? string.Empty;
            List<string> cvMatchedSkills = rules.RequiredSkills
                .Where(required => cvText.Contains(required, StringComparison.OrdinalIgnoreCase))
                .ToList();
            List<string> profileOnlyMatchedSkills = rules.RequiredSkills
                .Where(required =>
                    !cvMatchedSkills.Contains(required, StringComparer.OrdinalIgnoreCase)
                    && candidateSkills.Any(skill => string.Equals(skill, required, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            List<string> matchedSkills = cvMatchedSkills
                .Concat(profileOnlyMatchedSkills)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<string> cvMatchedPreferredSkills = rules.PreferredSkills
                .Where(required => cvText.Contains(required, StringComparison.OrdinalIgnoreCase))
                .ToList();
            List<string> profileOnlyPreferredSkills = rules.PreferredSkills
                .Where(required =>
                    !cvMatchedPreferredSkills.Contains(required, StringComparer.OrdinalIgnoreCase)
                    && candidateSkills.Any(skill => string.Equals(skill, required, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            List<string> matchedPreferredSkills = cvMatchedPreferredSkills
                .Concat(profileOnlyPreferredSkills)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool isAutoRejected = rules.AutoRejectRules.Any(rule =>
                MatchesAutoReject(candidate, rule));

            string? rejectReason = isAutoRejected
                ? rules.AutoRejectRules.First(rule => MatchesAutoReject(candidate, rule)).Reason
                : null;

            decimal requiredSkillEvidence = cvMatchedSkills.Count + (profileOnlyMatchedSkills.Count * 0.45m);
            decimal preferredSkillEvidence = cvMatchedPreferredSkills.Count + (profileOnlyPreferredSkills.Count * 0.35m);
            decimal skillScore = rules.RequiredSkills.Count == 0
                ? 28
                : Math.Min(40, Math.Round(requiredSkillEvidence / rules.RequiredSkills.Count * 40, 2));
            if (preferredSkillEvidence > 0)
            {
                skillScore = Math.Min(40, skillScore + Math.Round(preferredSkillEvidence * 2, 2));
            }
            decimal experienceScore = rules.MinExperienceYears.HasValue && rules.MinExperienceYears.Value > 0
                ? Math.Min(30, Math.Round((decimal)candidate.ExperienceYears / rules.MinExperienceYears.Value * 30, 2))
                : Math.Min(30, candidate.ExperienceYears * 6);
            decimal educationScore = string.IsNullOrWhiteSpace(candidate.Education) ? 8 : 18;
            decimal projectScore = Math.Min(10, (cvMatchedSkills.Count * 2) + (cvMatchedPreferredSkills.Count) + Math.Min(4, candidate.CvSummary.Length / 120m));
            decimal rawTotalScore = skillScore + experienceScore + educationScore + projectScore;
            decimal penaltyScore = rules.NegativeCriteria
                .Where(criteria => !criteria.AutoReject && IsNegativeHit(candidate, criteria))
                .Sum(GetPenaltyScore);
            decimal adjustedTotalScore = Math.Max(0, rawTotalScore - penaltyScore);
            decimal totalScore = isAutoRejected
                ? Math.Min(35, adjustedTotalScore)
                : Math.Min(100, adjustedTotalScore);

            List<string> weaknesses = [];
            weaknesses.AddRange(rules.RequiredSkills.Except(matchedSkills, StringComparer.OrdinalIgnoreCase).Select(skill => $"Missing {skill}"));
            weaknesses.AddRange(rules.NegativeCriteria
                .Where(criteria => IsNegativeHit(candidate, criteria))
                .Select(criteria => string.IsNullOrWhiteSpace(criteria.Label) ? $"Watch {criteria.Value}" : criteria.Label));
            if (rules.MinExperienceYears.HasValue && candidate.ExperienceYears < rules.MinExperienceYears.Value)
            {
                weaknesses.Add($"Below {rules.MinExperienceYears.Value} years experience");
            }

            return new CopilotRankingResultDto
            {
                CandidateUserId = candidate.CandidateUserId,
                ApplicationId = candidate.ApplicationId,
                FullName = candidate.FullName,
                TotalScore = totalScore,
                SkillScore = penaltyScore > 0
                    ? Math.Max(0, skillScore - Math.Min(skillScore, penaltyScore))
                    : skillScore,
                ExperienceScore = experienceScore,
                EducationScore = educationScore,
                ProjectScore = projectScore,
                Recommendation = isAutoRejected ? "Reject" : totalScore >= 80 ? "Interview" : totalScore >= 60 ? "Consider" : "Hold",
                IsAutoRejected = isAutoRejected,
                RejectReason = rejectReason,
                Strengths = matchedSkills.Count > 0 ? matchedSkills.Concat(matchedPreferredSkills).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : candidateSkills.Take(3).ToList(),
                Weaknesses = weaknesses.Take(4).ToList(),
                Summary = string.Empty,
                IsAiGenerated = false
            };
        })
        .OrderBy(result => result.IsAutoRejected)
        .ThenByDescending(result => result.TotalScore)
        .ThenByDescending(result => result.SkillScore)
        .ThenByDescending(result => result.ExperienceScore)
        .ToList();

        for (int i = 0; i < results.Count; i += 1)
        {
            results[i].RankPosition = i + 1;
        }

        return results;
    }

    /// <summary>
    /// Retrieves penalty score.
    /// </summary>
    /// <param name="criterion">The <paramref name="criterion"/> value.</param>
    /// <returns>The operation result.</returns>
    private static decimal GetPenaltyScore(CopilotRuleCriterionDto criterion)
    {
        return criterion.Weight.ToLowerInvariant() switch
        {
            "high" => 18,
            "low" => 6,
            _ => 12
        };
    }

    /// <summary>
    /// Executes the should run ranking operation.
    /// </summary>
    /// <param name="prompt">The <paramref name="prompt"/> value.</param>
    /// <param name="priorityCriteria">The <paramref name="priorityCriteria"/> value.</param>
    /// <param name="negativeCriteria">The <paramref name="negativeCriteria"/> value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    private static bool ShouldRunRanking(
        string prompt,
        IReadOnlyList<CopilotRuleCriterionRequestDto> priorityCriteria,
        IReadOnlyList<CopilotRuleCriterionRequestDto> negativeCriteria)
    {
        if (priorityCriteria.Count > 0 || negativeCriteria.Count > 0)
        {
            return true;
        }

        string lowered = prompt.ToLowerInvariant();
        string[] explicitRankingPhrases =
        [
            "rank candidates",
            "rank cvs",
            "score candidates",
            "evaluate candidates",
            "screen candidates",
            "shortlist candidates",
            "top candidates",
            "xếp hạng ứng viên",
            "xếp hạng cv",
            "đánh giá ứng viên",
            "chấm ứng viên",
            "lọc ứng viên",
            "xếp loại ứng viên",
            "so sánh ứng viên",
            "shortlist cv",
            "rank applicant",
            "evaluate applicant"
        ];

        if (explicitRankingPhrases.Any(keyword => lowered.Contains(keyword)))
        {
            return true;
        }

        bool hasQuestionStyleIntent =
            lowered.Contains("như thế nào")
            || lowered.Contains("nên làm gì")
            || lowered.StartsWith("tôi nên")
            || lowered.StartsWith("mình nên")
            || lowered.StartsWith("how should")
            || lowered.StartsWith("what should");

        if (hasQuestionStyleIntent)
        {
            return false;
        }

        string[] rankingActionKeywords =
        [
            "rank", "ranking", "score", "evaluate", "assessment", "screen", "screening",
            "shortlist", "top", "xếp hạng", "đánh giá", "chấm", "lọc", "xếp loại"
        ];
        string[] candidateTargetKeywords =
        [
            "candidate", "candidates", "applicant", "applicants", "cv", "resume", "ứng viên", "hồ sơ"
        ];

        return rankingActionKeywords.Any(action => lowered.Contains(action))
            && candidateTargetKeywords.Any(target => lowered.Contains(target));
    }

    /// <summary>
    /// Builds ranking assistant message.
    /// </summary>
    /// <param name="results">The <paramref name="results"/> value.</param>
    /// <returns>The resulting string value.</returns>
    private static string BuildRankingAssistantMessage(IReadOnlyList<CopilotRankingResultDto> results)
    {
        List<string> lines = results
            .Where(result => !string.IsNullOrWhiteSpace(result.Summary) || result.IsAutoRejected)
            .Take(5)
            .Select(result =>
            {
                string reason = !string.IsNullOrWhiteSpace(result.Summary)
                    ? result.Summary.Trim()
                    : result.RejectReason ?? "Did not meet the active criteria.";
                string prefix = result.IsAutoRejected
                    ? $"Rejected - {result.FullName}:"
                    : $"{result.RankPosition}. {result.FullName}:";
                return $"{prefix} {reason}";
            })
            .ToList();

        return lines.Count > 0
            ? string.Join("\n", lines)
            : "Ranking completed.";
    }

    /// <summary>
    /// Normalizes ranking results.
    /// </summary>
    /// <param name="results">The <paramref name="results"/> value.</param>
    /// <param name="candidates">The <paramref name="candidates"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<CopilotRankingResultDto> NormalizeRankingResults(
        IReadOnlyList<CopilotRankingResultDto> results,
        IReadOnlyList<CopilotCandidateDto> candidates)
    {
        Dictionary<Guid, CopilotCandidateDto> candidateLookup = candidates
            .GroupBy(candidate => candidate.CandidateUserId)
            .ToDictionary(group => group.Key, group => group.First());

        List<CopilotRankingResultDto> normalized = results
            .Where(result => candidateLookup.ContainsKey(result.CandidateUserId))
            .GroupBy(result => result.CandidateUserId)
            .Select(group =>
            {
                CopilotRankingResultDto chosen = group
                    .OrderBy(result => result.IsAutoRejected)
                    .ThenBy(result => result.RankPosition <= 0 ? int.MaxValue : result.RankPosition)
                    .ThenByDescending(result => result.TotalScore)
                    .First();

                CopilotCandidateDto candidate = candidateLookup[group.Key];
                chosen.ApplicationId = chosen.ApplicationId == Guid.Empty
                    ? candidate.ApplicationId
                    : chosen.ApplicationId;
                chosen.FullName = string.IsNullOrWhiteSpace(chosen.FullName)
                    ? candidate.FullName
                    : chosen.FullName;

                return chosen;
            })
            .OrderBy(result => result.IsAutoRejected)
            .ThenBy(result => result.RankPosition <= 0 ? int.MaxValue : result.RankPosition)
            .ThenByDescending(result => result.TotalScore)
            .ToList();

        for (int index = 0; index < normalized.Count; index += 1)
        {
            normalized[index].RankPosition = index + 1;
        }

        return normalized;
    }

    /// <summary>
    /// Merges rules.
    /// </summary>
    /// <param name="previousRules">The <paramref name="previousRules"/> value.</param>
    /// <param name="currentRules">The <paramref name="currentRules"/> value.</param>
    /// <returns>The operation result.</returns>
    private static CopilotNormalizedRulesDto MergeRules(CopilotNormalizedRulesDto previousRules, CopilotNormalizedRulesDto currentRules)
    {
        return new CopilotNormalizedRulesDto
        {
            RequiredSkills = previousRules.RequiredSkills
                .Concat(currentRules.RequiredSkills)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            PreferredSkills = previousRules.PreferredSkills
                .Concat(currentRules.PreferredSkills)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            MinExperienceYears = currentRules.MinExperienceYears ?? previousRules.MinExperienceYears,
            MinTotalScore = currentRules.MinTotalScore ?? previousRules.MinTotalScore,
            AutoRejectRules = previousRules.AutoRejectRules
                .Concat(currentRules.AutoRejectRules)
                .GroupBy(rule => $"{rule.Field}|{rule.Operator}|{rule.Value}|{rule.Reason}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList(),
            PriorityCriteria = DeduplicateCriteria(previousRules.PriorityCriteria.Concat(currentRules.PriorityCriteria).ToList()),
            NegativeCriteria = DeduplicateCriteria(previousRules.NegativeCriteria.Concat(currentRules.NegativeCriteria).ToList())
        };
    }

    /// <summary>
    /// Deserializes rules.
    /// </summary>
    /// <param name="json">The <paramref name="json"/> value.</param>
    /// <returns>The operation result.</returns>
    private static CopilotNormalizedRulesDto DeserializeRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CopilotNormalizedRulesDto();
        }

        return JsonSerializer.Deserialize<CopilotNormalizedRulesDto>(json) ?? new CopilotNormalizedRulesDto();
    }

    /// <summary>
    /// Deserializes string list.
    /// </summary>
    /// <param name="json">The <paramref name="json"/> value.</param>
    /// <returns>The operation result.</returns>
    private static IReadOnlyList<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    /// <summary>
    /// Deserializes summary.
    /// </summary>
    /// <param name="json">The <paramref name="json"/> value.</param>
    /// <returns>The resulting string value.</returns>
    private static string DeserializeSummary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("Summary", out JsonElement summary)
            ? summary.GetString() ?? string.Empty
            : document.RootElement.TryGetProperty("summary", out JsonElement summaryLower)
                ? summaryLower.GetString() ?? string.Empty
                : string.Empty;
    }

    /// <summary>
    /// Deserializes is ai generated.
    /// </summary>
    /// <param name="json">The <paramref name="json"/> value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    private static bool DeserializeIsAiGenerated(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("IsAiGenerated", out JsonElement value)
            ? value.GetBoolean()
            : document.RootElement.TryGetProperty("isAiGenerated", out JsonElement valueLower)
                && valueLower.GetBoolean();
    }

    /// <summary>
    /// Deduplicates criteria.
    /// </summary>
    /// <param name="criteria">The <paramref name="criteria"/> value.</param>
    /// <returns>The operation result.</returns>
    private static IReadOnlyList<CopilotRuleCriterionDto> DeduplicateCriteria(IReadOnlyList<CopilotRuleCriterionDto> criteria)
    {
        return criteria
            .GroupBy(item => $"{item.Field}|{item.Operator}|{item.Value}|{item.Weight}|{item.AutoReject}|{item.Label}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    /// <summary>
    /// Executes the matches auto reject operation.
    /// </summary>
    /// <param name="candidate">The <paramref name="candidate"/> value.</param>
    /// <param name="rule">The <paramref name="rule"/> value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    private static bool MatchesAutoReject(CopilotCandidateDto candidate, CopilotAutoRejectRuleDto rule)
    {
        string? value = rule.Field.ToLowerInvariant() switch
        {
            "education" => candidate.Education,
            "cvsummary" => candidate.CvSummary,
            "fullname" => candidate.FullName,
            _ => null
        };

        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(rule.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Executes the is negative hit operation.
    /// </summary>
    /// <param name="candidate">The <paramref name="candidate"/> value.</param>
    /// <param name="criterion">The <paramref name="criterion"/> value.</param>
    /// <returns>A value indicating whether the operation succeeded.</returns>
    private static bool IsNegativeHit(CopilotCandidateDto candidate, CopilotRuleCriterionDto criterion)
    {
        return criterion.Field.ToLowerInvariant() switch
        {
            "education" => !string.IsNullOrWhiteSpace(candidate.Education)
                && candidate.Education.Contains(criterion.Value, StringComparison.OrdinalIgnoreCase),
            "cvsummary" => candidate.CvSummary.Contains(criterion.Value, StringComparison.OrdinalIgnoreCase),
            "experienceyears" => int.TryParse(criterion.Value, out int years) && candidate.ExperienceYears < years,
            _ => false
        };
    }
}
