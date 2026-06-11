using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
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
    private readonly IFileStorageService _fileStorageService;
    private readonly IResumeTextExtractor _resumeTextExtractor;
    private readonly IAiCopilotProvider _aiCopilotProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OpenAiSettings _openAiSettings;

    public CopilotService(
        ICopilotRepository copilotRepository,
        IFileStorageService fileStorageService,
        IResumeTextExtractor resumeTextExtractor,
        IAiCopilotProvider aiCopilotProvider,
        IUnitOfWork unitOfWork,
        IOptions<OpenAiSettings> openAiOptions)
    {
        _copilotRepository = copilotRepository;
        _fileStorageService = fileStorageService;
        _resumeTextExtractor = resumeTextExtractor;
        _aiCopilotProvider = aiCopilotProvider;
        _unitOfWork = unitOfWork;
        _openAiSettings = openAiOptions.Value;
    }

    public async Task<ApiResponse<IReadOnlyList<CopilotJobOptionDto>>> GetJobsAsync()
    {
        IReadOnlyList<CopilotJobOptionDto> jobs = await _copilotRepository.GetJobOptionsAsync();
        return ApiResponse<IReadOnlyList<CopilotJobOptionDto>>.Ok(jobs);
    }

    public async Task<ApiResponse<CopilotConversationDto>> CreateConversationAsync(CreateCopilotConversationRequest request, Guid userId)
    {
        CopilotConversation? existing = await _copilotRepository.GetLatestConversationAsync(request.JobId, userId);
        if (existing is not null)
        {
            return ApiResponse<CopilotConversationDto>.Ok(MapConversation(existing));
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

        return ApiResponse<CopilotConversationDto>.Created(MapConversation(conversation));
    }

    public async Task<ApiResponse<CopilotCandidatePoolDto>> GetCandidatePoolAsync(Guid jobId)
    {
        CopilotCandidatePoolDto? pool = await _copilotRepository.GetCandidatePoolAsync(jobId);
        return pool is null
            ? ApiResponse<CopilotCandidatePoolDto>.NotFound("Job not found")
            : ApiResponse<CopilotCandidatePoolDto>.Ok(pool);
    }

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

        pool = await EnrichPoolWithResumeTextAsync(pool);
        CopilotNormalizedRulesDto rules = BuildRules(request.Prompt, pool.Job.RequiredSkills);
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
            results = aiResponse.Results
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
            ModelName = "deterministic-copilot-v1",
            Results = results.Select(result => new CopilotRankingResult
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
                ExplanationJson = JsonSerializer.Serialize(new { result.Summary })
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
        conversation.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<CopilotPromptResponseDto>.Ok(new CopilotPromptResponseDto
        {
            ConversationId = conversationId,
            RankingSessionId = session.Id,
            NormalizedRules = rules,
            Results = results
        });
    }

    private async Task<CopilotCandidatePoolDto> EnrichPoolWithResumeTextAsync(CopilotCandidatePoolDto pool)
    {
        int maxChars = _openAiSettings.MaxResumeCharsPerCandidate > 0
            ? _openAiSettings.MaxResumeCharsPerCandidate
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

    private static CopilotConversationDto MapConversation(CopilotConversation conversation)
    {
        return new CopilotConversationDto
        {
            ConversationId = conversation.Id,
            JobId = conversation.JobId,
            Title = conversation.Title ?? "AI Recruitment Copilot",
            LatestRankingSessionId = conversation.LatestRankingSessionId
        };
    }

    private static CopilotNormalizedRulesDto BuildRules(string prompt, IReadOnlyList<string> jobRequiredSkills)
    {
        string loweredPrompt = prompt.ToLowerInvariant();
        List<string> requiredSkills = jobRequiredSkills
            .Where(skill => loweredPrompt.Contains(skill.ToLowerInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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

        return new CopilotNormalizedRulesDto
        {
            RequiredSkills = requiredSkills,
            MinExperienceYears = minExperienceYears,
            AutoRejectRules = autoRejectRules
        };
    }

    private static List<CopilotRankingResultDto> RankCandidates(IReadOnlyList<CopilotCandidateDto> candidates, CopilotNormalizedRulesDto rules)
    {
        List<CopilotRankingResultDto> results = candidates.Select(candidate =>
        {
            List<string> candidateSkills = candidate.Skills.ToList();
            List<string> matchedSkills = rules.RequiredSkills
                .Where(required => candidateSkills.Any(skill => string.Equals(skill, required, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            bool isAutoRejected = rules.AutoRejectRules.Any(rule =>
                rule.Field.Equals("education", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(candidate.Education)
                && candidate.Education.Contains(rule.Value, StringComparison.OrdinalIgnoreCase));

            string? rejectReason = isAutoRejected
                ? rules.AutoRejectRules.First().Reason
                : null;

            decimal skillScore = rules.RequiredSkills.Count == 0
                ? 40
                : Math.Round((decimal)matchedSkills.Count / rules.RequiredSkills.Count * 40, 2);
            decimal experienceScore = rules.MinExperienceYears.HasValue && rules.MinExperienceYears.Value > 0
                ? Math.Min(30, Math.Round((decimal)candidate.ExperienceYears / rules.MinExperienceYears.Value * 30, 2))
                : Math.Min(30, candidate.ExperienceYears * 6);
            decimal educationScore = string.IsNullOrWhiteSpace(candidate.Education) ? 8 : 18;
            decimal projectScore = Math.Min(10, candidate.CvSummary.Length / 30);
            decimal totalScore = isAutoRejected ? Math.Min(35, skillScore + experienceScore + educationScore + projectScore) : Math.Min(100, skillScore + experienceScore + educationScore + projectScore);

            List<string> weaknesses = [];
            weaknesses.AddRange(rules.RequiredSkills.Except(matchedSkills, StringComparer.OrdinalIgnoreCase).Select(skill => $"Missing {skill}"));
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
                SkillScore = skillScore,
                ExperienceScore = experienceScore,
                EducationScore = educationScore,
                ProjectScore = projectScore,
                Recommendation = isAutoRejected ? "Reject" : totalScore >= 80 ? "Interview" : totalScore >= 60 ? "Consider" : "Hold",
                IsAutoRejected = isAutoRejected,
                RejectReason = rejectReason,
                Strengths = matchedSkills.Count > 0 ? matchedSkills : candidateSkills.Take(3).ToList(),
                Weaknesses = weaknesses.Take(4).ToList(),
                Summary = isAutoRejected ? $"Rejected by rule: {rejectReason}" : "Ranked by skill, experience, education, and CV summary match."
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
}
