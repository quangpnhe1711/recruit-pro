using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Common;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.DTOs.Request.Applications;
using RecruitPro.Application.DTOs.Request.Copilot;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Copilot;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Services;

public class CopilotService : ICopilotService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // v2: only applications in the CV-screening stage are rank-eligible. The ATS enum name is
    // "Screening" (see ApplicationStatus / ApplicationStatusWorkflow). Candidates already at
    // ManagerReview (Head Review), Interview, Offer, Hired, Rejected, OfferDeclined or Withdrawn are
    // excluded from default ranking.
    private static readonly string ScreeningStatus = ApplicationStatus.Screening.ToString();

    private readonly ICopilotRepository _copilotRepository;
    private readonly IJobRepository _jobRepository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IApplicationService _applicationService;
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
        IApplicationRepository applicationRepository,
        IApplicationService applicationService,
        IFileStorageService fileStorageService,
        IResumeTextExtractor resumeTextExtractor,
        IAiCopilotProvider aiCopilotProvider,
        IUnitOfWork unitOfWork,
        IOptions<AiProviderSettings> aiProviderOptions,
        IMapper mapper)
    {
        _copilotRepository = copilotRepository;
        _jobRepository = jobRepository;
        _applicationRepository = applicationRepository;
        _applicationService = applicationService;
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
    public async Task<ApiResponse<IReadOnlyList<CopilotJobOptionDto>>> GetJobsAsync(Guid callerUserId)
    {
        // Only surface jobs the caller owns so the picker never offers a job whose candidate pool the
        // caller would be forbidden from opening (mirrors OwnershipScope.CanAccessJob).
        IReadOnlyList<CopilotJobOptionDto> jobs = await _copilotRepository.GetJobOptionsAsync(callerUserId);
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
            return ApiResponse<CopilotConversationDetailDto>.NotFound(ErrorCodes.EntityNotFound);
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
            return ApiResponse<CopilotCandidatePoolDto>.NotFound(ErrorCodes.JobNotFound);

        if (!OwnershipScope.CanAccessJob(job, callerUserId, callerRoles))
            return ApiResponse<CopilotCandidatePoolDto>.Forbidden(ErrorCodes.Forbidden);

        CopilotCandidatePoolDto? pool = await _copilotRepository.GetCandidatePoolAsync(jobId);
        return pool is null
            ? ApiResponse<CopilotCandidatePoolDto>.NotFound(ErrorCodes.JobNotFound)
            : ApiResponse<CopilotCandidatePoolDto>.Ok(pool);
    }

    public async Task<ApiResponse<NaturalLanguageCandidateSearchResponseDto>> SearchCandidatesAsync(
        NaturalLanguageCandidateSearchRequest request,
        Guid? callerUserId,
        IReadOnlyCollection<string> callerRoles)
    {
        if (request.JobId == Guid.Empty)
        {
            return ApiResponse<NaturalLanguageCandidateSearchResponseDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return ApiResponse<NaturalLanguageCandidateSearchResponseDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        // v2 §1 — candidate search is no longer a first-class active flow. Ranking is the single
        // source of truth for screening evaluation. This endpoint is soft-deprecated: it returns a
        // deterministic view of the screening pool only, never calls the AI provider, and never
        // persists a new artifact. The frontend no longer surfaces it.
        ApiResponse<CopilotCandidatePoolDto> poolResponse = await GetCandidatePoolAsync(request.JobId, callerUserId, callerRoles);
        if (!poolResponse.Success || poolResponse.Data is null)
        {
            return MirrorFailure<NaturalLanguageCandidateSearchResponseDto>(poolResponse.StatusCode, poolResponse.Message, poolResponse.ErrorCode);
        }

        CopilotCandidatePoolDto pool = FilterToScreeningPool(poolResponse.Data);
        CopilotNormalizedRulesDto rules = BuildRules(request.Query, pool.Job.RequiredSkills, [], [], []);
        List<CopilotRankingResultDto> ranked = RankCandidates(pool.Candidates, rules);
        ApplyVietnameseFitEvaluation(ranked, rules);
        int maxResults = Math.Clamp(request.MaxResults <= 0 ? 10 : request.MaxResults, 1, 25);

        IReadOnlyList<CopilotCandidateSearchResultDto> results = ranked
            .Take(maxResults)
            .Select(result => BuildSearchResult(result, rules))
            .ToList();

        NaturalLanguageCandidateSearchResponseDto response = new()
        {
            Query = request.Query.Trim(),
            ExtractedFilters = rules,
            Results = results,
            Ai = BuildDeterministicMetadata(
                "candidate-search:deprecated — tính năng tìm ứng viên bằng ngôn ngữ tự nhiên đã ngừng hoạt động trong luồng v2; hãy dùng chức năng xếp hạng ứng viên.")
        };

        return ApiResponse<NaturalLanguageCandidateSearchResponseDto>.Ok(response);
    }

    public async Task<ApiResponse<CandidateFitAnalysisResponseDto>> AnalyzeCandidateFitAsync(
        Guid jobId,
        CandidateFitAnalysisRequest request,
        Guid? callerUserId,
        IReadOnlyCollection<string> callerRoles)
    {
        // v2 §9 — fit analysis DERIVES FROM the latest ranking result. It never re-ranks, never
        // reorders and never makes an independent provider call: the detailed Vietnamese fit
        // evaluation is produced at ranking time and simply read back here.
        ApiResponse<CopilotCandidatePoolDto> poolResponse = await GetCandidatePoolAsync(jobId, callerUserId, callerRoles);
        if (!poolResponse.Success || poolResponse.Data is null)
        {
            return MirrorFailure<CandidateFitAnalysisResponseDto>(poolResponse.StatusCode, poolResponse.Message, poolResponse.ErrorCode);
        }

        CopilotRankingSession? session = callerUserId.HasValue
            ? await _copilotRepository.GetLatestRankingSessionForJobAsync(jobId, callerUserId.Value)
            : null;

        if (session is null)
        {
            return ApiResponse<CandidateFitAnalysisResponseDto>.Ok(new CandidateFitAnalysisResponseDto
            {
                JobId = jobId,
                Analyses = [],
                Ai = BuildDeterministicMetadata(
                    "Chưa có kết quả xếp hạng cho vị trí này. Hãy chạy xếp hạng ứng viên trước để có phân tích độ phù hợp.")
            });
        }

        CopilotRankingSessionDetailDto detail = _mapper.Map<CopilotRankingSessionDetailDto>(session);
        HashSet<Guid> candidateFilter = request.CandidateUserIds.Where(id => id != Guid.Empty).ToHashSet();
        HashSet<Guid> applicationFilter = request.ApplicationIds.Where(id => id != Guid.Empty).ToHashSet();

        List<CandidateFitAnalysisDto> analyses = detail.Results
            .Where(result =>
                candidateFilter.Count == 0 && applicationFilter.Count == 0
                || candidateFilter.Contains(result.CandidateUserId)
                || applicationFilter.Contains(result.ApplicationId))
            .Select(BuildFitAnalysis)
            .ToList();

        CandidateFitAnalysisResponseDto response = new()
        {
            JobId = jobId,
            Analyses = analyses,
            Ai = BuildDeterministicMetadata(
                "fit-analysis:derived-from-ranking — độ phù hợp được suy ra từ kết quả xếp hạng gần nhất, không chấm lại.")
        };

        return ApiResponse<CandidateFitAnalysisResponseDto>.Ok(response);
    }

    public async Task<ApiResponse<InterviewQuestionSetDto>> GenerateInterviewQuestionsAsync(
        Guid jobId,
        InterviewQuestionRequest request,
        Guid? callerUserId,
        IReadOnlyCollection<string> callerRoles)
    {
        ApiResponse<CopilotCandidatePoolDto> poolResponse = await GetCandidatePoolAsync(jobId, callerUserId, callerRoles);
        if (!poolResponse.Success || poolResponse.Data is null)
        {
            return MirrorFailure<InterviewQuestionSetDto>(poolResponse.StatusCode, poolResponse.Message, poolResponse.ErrorCode);
        }

        CopilotCandidatePoolDto pool = poolResponse.Data;
        CopilotCandidateDto? candidate = ResolveCandidate(pool, request.CandidateUserId, request.ApplicationId);
        int questionCount = Math.Clamp(request.QuestionCount <= 0 ? 8 : request.QuestionCount, 3, 12);
        string focus = string.IsNullOrWhiteSpace(request.Focus) ? "job-fit" : request.Focus.Trim();

        // v2: interview questions are generated ONCE per candidate and cached as an artifact. A repeated
        // click returns the previously generated set instead of calling the AI provider again.
        if (callerUserId.HasValue && request.ApplicationId.HasValue && request.ApplicationId.Value != Guid.Empty)
        {
            IReadOnlyList<CopilotGeneratedArtifact> existing = await _copilotRepository.GetGeneratedArtifactsAsync(
                callerUserId.Value, jobId, request.ApplicationId, "interview_questions", 1);
            CopilotGeneratedArtifact? cached = existing.FirstOrDefault();
            if (cached is not null && !string.IsNullOrWhiteSpace(cached.PayloadJson))
            {
                InterviewQuestionSetDto? cachedSet = null;
                try
                {
                    cachedSet = JsonSerializer.Deserialize<InterviewQuestionSetDto>(cached.PayloadJson, JsonOptions);
                }
                catch (JsonException)
                {
                    // Corrupt payload — fall through and regenerate.
                }

                if (cachedSet is not null && cachedSet.Questions.Count > 0)
                {
                    cachedSet.Ai ??= new CopilotAiMetadataDto();
                    cachedSet.Ai.Warnings = cachedSet.Ai.Warnings.Concat(["interview-questions:cached"]).ToList();
                    return ApiResponse<InterviewQuestionSetDto>.Ok(cachedSet);
                }
            }
        }

        List<InterviewQuestionDto> questions = BuildInterviewQuestions(pool.Job, candidate, focus)
            .Take(questionCount)
            .ToList();

        InterviewQuestionSetDto response = new()
        {
            JobId = jobId,
            CandidateUserId = candidate?.CandidateUserId,
            Focus = focus,
            Questions = questions,
            Ai = BuildDeterministicMetadata("Question pack uses job skills, requirements, and candidate profile evidence as deterministic fallback.")
        };

        response = await TryGenerateStructuredResponseAsync(
            "interview_questions",
            "interview_questions",
            callerUserId,
            QuestionInstructions(questionCount),
            new Dictionary<string, string>
            {
                ["jobTitle"] = pool.Job.Title,
                ["focus"] = focus,
                ["candidateName"] = candidate?.FullName ?? string.Empty
            },
            new
            {
                job = pool.Job,
                candidate,
                focus,
                questionCount,
                deterministicQuestions = questions
            },
            response,
            ValidateQuestionSet,
            generated =>
            {
                generated.JobId = jobId;
                generated.CandidateUserId = candidate?.CandidateUserId;
                generated.Focus = string.IsNullOrWhiteSpace(generated.Focus) ? focus : generated.Focus.Trim();
                generated.Questions = generated.Questions.Take(questionCount).ToList();
            },
            generated => generated.Ai,
            (generated, metadata) => generated.Ai = metadata);

        await PersistArtifactAsync(
            callerUserId,
            jobId,
            request.ApplicationId,
            "interview_questions",
            focus,
            response,
            response.Ai);

        return ApiResponse<InterviewQuestionSetDto>.Ok(response);
    }

    public async Task<ApiResponse<HrEmailDraftResponseDto>> DraftApplicationEmailAsync(
        Guid applicationId,
        HrEmailDraftRequest request,
        Guid? callerUserId,
        IReadOnlyCollection<string> callerRoles)
    {
        RecruitPro.Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(applicationId);
        if (application is null)
        {
            return ApiResponse<HrEmailDraftResponseDto>.NotFound(ErrorCodes.ApplicationNotFound);
        }

        if (!OwnershipScope.CanAccessApplication(application, callerUserId, callerRoles))
        {
            return ApiResponse<HrEmailDraftResponseDto>.Forbidden(ErrorCodes.Forbidden);
        }

        string candidateName = string.IsNullOrWhiteSpace(application.User?.FullName) ? "ứng viên" : application.User.FullName;
        string jobTitle = string.IsNullOrWhiteSpace(application.Job?.Title) ? "vị trí đang ứng tuyển" : application.Job.Title;
        string templateType = string.IsNullOrWhiteSpace(request.TemplateType) ? "screening_follow_up" : request.TemplateType.Trim();
        string tone = string.IsNullOrWhiteSpace(request.Tone) ? "professional" : request.Tone.Trim();

        // v2 §2 — AI email drafting is removed from the active flow. This endpoint no longer calls the
        // AI provider and no longer persists a new artifact; it returns a deterministic template only.
        (string subject, string body) = BuildEmailDraft(candidateName, jobTitle, templateType, tone, request.AdditionalInstruction);

        HrEmailDraftResponseDto response = new()
        {
            ApplicationId = applicationId,
            TemplateType = templateType,
            Subject = subject,
            Body = body,
            Evidence =
            [
                $"Ứng viên: {candidateName}",
                $"Vị trí: {jobTitle}",
                $"Trạng thái hiện tại: {application.Status}"
            ],
            Ai = BuildDeterministicMetadata(
                "email-draft:deprecated — soạn email bằng AI đã ngừng trong luồng v2; nội dung dưới đây chỉ là mẫu tất định.")
        };

        return ApiResponse<HrEmailDraftResponseDto>.Ok(response);
    }

    public async Task<ApiResponse<IReadOnlyList<CopilotPromptTemplateDto>>> GetPromptTemplatesAsync(Guid userId)
    {
        IReadOnlyList<CopilotPromptTemplate> templates = await _copilotRepository.GetPromptTemplatesAsync(userId);
        return ApiResponse<IReadOnlyList<CopilotPromptTemplateDto>>.Ok(templates.Select(MapPromptTemplate).ToList());
    }

    public async Task<ApiResponse<CopilotPromptTemplateDto>> CreatePromptTemplateAsync(CreateCopilotPromptTemplateRequest request, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResponse<CopilotPromptTemplateDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return ApiResponse<CopilotPromptTemplateDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        CopilotPromptTemplate template = new()
        {
            OwnerUserId = userId,
            Name = request.Name.Trim(),
            TemplateType = string.IsNullOrWhiteSpace(request.TemplateType) ? "general" : request.TemplateType.Trim(),
            Prompt = request.Prompt.Trim(),
            IsActive = request.IsActive,
            UpdatedAt = DbDateTime.Now
        };

        await _copilotRepository.AddPromptTemplateAsync(template);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<CopilotPromptTemplateDto>.Created(MapPromptTemplate(template));
    }

    public async Task<ApiResponse<CandidateFitAnalysisSnapshotDto>> GetLatestFitAnalysisForApplicationAsync(
        Guid applicationId,
        Guid? callerUserId,
        IReadOnlyCollection<string> callerRoles)
    {
        RecruitPro.Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(applicationId);
        if (application is null)
        {
            return ApiResponse<CandidateFitAnalysisSnapshotDto>.NotFound(ErrorCodes.ApplicationNotFound);
        }

        if (!OwnershipScope.CanAccessApplication(application, callerUserId, callerRoles))
        {
            return ApiResponse<CandidateFitAnalysisSnapshotDto>.Forbidden(ErrorCodes.Forbidden);
        }

        CandidateFitAnalysis? analysis = await _copilotRepository.GetLatestFitAnalysisAsync(applicationId);
        if (analysis is null)
        {
            return ApiResponse<CandidateFitAnalysisSnapshotDto>.NotFound(ErrorCodes.EntityNotFound);
        }

        return ApiResponse<CandidateFitAnalysisSnapshotDto>.Ok(MapFitAnalysisSnapshot(analysis));
    }

    public async Task<ApiResponse<IReadOnlyList<CopilotGeneratedArtifactDto>>> GetGeneratedArtifactsAsync(
        Guid ownerUserId,
        Guid? jobId,
        Guid? applicationId,
        string? artifactType,
        int take)
    {
        IReadOnlyList<CopilotGeneratedArtifact> artifacts = await _copilotRepository.GetGeneratedArtifactsAsync(
            ownerUserId,
            jobId == Guid.Empty ? null : jobId,
            applicationId == Guid.Empty ? null : applicationId,
            artifactType,
            Math.Clamp(take <= 0 ? 20 : take, 1, 50));

        return ApiResponse<IReadOnlyList<CopilotGeneratedArtifactDto>>.Ok(artifacts.Select(MapGeneratedArtifact).ToList());
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
            return ApiResponse<CopilotPromptResponseDto>.NotFound(ErrorCodes.EntityNotFound);
        }

        bool shouldRunRanking = request.ForceRanking
            && (!string.IsNullOrWhiteSpace(request.Prompt)
                || request.PriorityCriteria.Count > 0
                || request.NegativeCriteria.Count > 0);

        // Chat path (not a ranking run). The scope guard runs BEFORE any candidate-pool load, resume
        // enrichment, provider call or persistence: an unrelated question is refused immediately in
        // Vietnamese without touching data or AI (v2 §14).
        if (!shouldRunRanking)
        {
            return await HandleChatReplyAsync(conversation, conversationId, request, userId);
        }

        CopilotCandidatePoolDto? fullPool = await _copilotRepository.GetCandidatePoolAsync(request.JobId);
        if (fullPool is null)
        {
            return ApiResponse<CopilotPromptResponseDto>.NotFound(ErrorCodes.JobNotFound);
        }

        IReadOnlyList<CopilotSavedRule> savedRules = await _copilotRepository.GetSavedRulesAsync(request.JobId, userId);
        IReadOnlyList<CopilotSavedRule> activeSavedRules = savedRules.Where(rule => rule.IsActive).ToList();

        // Merge the latest ranking context, if requested, so the effective rules match what will run.
        CopilotNormalizedRulesDto? latestContextRules = null;
        if (request.UseLatestRankingContext && conversation.LatestRankingSessionId.HasValue)
        {
            CopilotRankingSession? latestSession = await _copilotRepository.GetRankingSessionAsync(conversation.LatestRankingSessionId.Value);
            if (latestSession is not null)
            {
                latestContextRules = DeserializeRules(latestSession.NormalizedRulesJson);
            }
        }

        // v2 §8 — idempotency: fingerprint the EFFECTIVE ranking input. We hash the effective merged
        // rules (prompt/criteria/saved-rules/latest-context all fold into these) plus the screening
        // pool evidence, NOT the raw latest-context blob. This keeps the fingerprint stable across an
        // identical re-click: after the first run the latest session already stores these merged rules,
        // so merging them back in produces the same rules and therefore the same hash. If a completed
        // session with the same fingerprint exists, return it WITHOUT calling the AI provider or
        // creating a duplicate.
        CopilotCandidatePoolDto screeningPool = FilterToScreeningPool(fullPool);
        CopilotNormalizedRulesDto effectiveRules = BuildRules(
            request.Prompt,
            screeningPool.Job.RequiredSkills,
            request.PriorityCriteria,
            request.NegativeCriteria,
            activeSavedRules);
        if (latestContextRules is not null)
        {
            effectiveRules = MergeRules(latestContextRules, effectiveRules);
        }

        string inputHash = ComputeRankingInputHash(request.JobId, userId, effectiveRules, screeningPool);

        CopilotRankingSession? matching = await _copilotRepository.GetLatestMatchingRankingSessionAsync(request.JobId, userId, inputHash);
        if (matching is not null)
        {
            CopilotRankingSessionDetailDto detail = _mapper.Map<CopilotRankingSessionDetailDto>(matching);
            return ApiResponse<CopilotPromptResponseDto>.Ok(new CopilotPromptResponseDto
            {
                ConversationId = conversationId,
                RankingSessionId = matching.Id,
                DidRank = true,
                ReusedRankingSession = true,
                AssistantMessage = "Tiêu chí chưa thay đổi nên hệ thống đang hiển thị lại kết quả xếp hạng mới nhất.",
                NormalizedRules = detail.NormalizedRules,
                Results = detail.Results,
                Warnings = ["ranking-session:reused"]
            });
        }

        CopilotRankingCoreResult core = await RunRankingCoreAsync(
            screeningPool,
            callerUserId: userId,
            prompt: request.Prompt,
            priorityCriteria: request.PriorityCriteria,
            negativeCriteria: request.NegativeCriteria,
            savedRules: activeSavedRules,
            latestContextRules: latestContextRules,
            conversationId: conversationId);

        List<CopilotRankingResultDto> results = core.Results.ToList();
        CopilotNormalizedRulesDto rules = core.Rules;

        CopilotRankingSession session = new()
        {
            JobId = request.JobId,
            ConversationId = conversationId,
            UserId = userId,
            UserPrompt = request.Prompt,
            NormalizedRulesJson = JsonSerializer.Serialize(rules),
            InputHash = inputHash,
            TotalCandidates = screeningPool.Candidates.Count,
            ModelName = results.Any(result => result.IsAiGenerated)
                ? _aiProviderSettings.Model
                : "deterministic-copilot-v2",
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
                ExplanationJson = JsonSerializer.Serialize(new
                {
                    result.Summary,
                    result.IsAiGenerated,
                    result.FitLabel,
                    result.ConfidenceScore,
                    result.Evidence
                })
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
            Content = BuildRankingAssistantMessage(results),
            MetadataJson = JsonSerializer.Serialize(new { rules, resultCount = results.Count }),
            SequenceNo = nextSequence + 1
        });

        await _copilotRepository.AddRankingSessionAsync(session);
        await _unitOfWork.SaveChangesAsync();

        // v2 §4/§9 — fit-style evaluation is created during ranking and persisted so the candidate
        // review-detail screen and the (now read-only) fit endpoint can read it without re-ranking.
        await PersistFitSnapshotsAsync(request.JobId, core.Ai.AuditId, core.Ai, results);

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
            Results = results,
            Warnings = core.Ai.Warnings
        });
    }

    /// <summary>
    /// Non-ranking chat reply. Scope (v2 §14) is judged by the AI itself from job/conversation
    /// semantics — see the system prompt in <see cref="IAiCopilotProvider.TryCreateChatReplyAsync"/> —
    /// not by a backend keyword gate: every conversation is already bound to one job/JD, so a keyword
    /// allowlist/denylist can never exhaustively cover legitimate recruitment phrasing (JD, tiêu chí,
    /// "viết code Java phù hợp không", ...). The backend only enforces deterministic checks: prompt
    /// presence, conversation ownership/JobId binding (done by the caller), and job/candidate access.
    /// </summary>
    private async Task<ApiResponse<CopilotPromptResponseDto>> HandleChatReplyAsync(
        CopilotConversation conversation,
        Guid conversationId,
        CopilotPromptRequest request,
        Guid userId)
    {
        int replySequence = await _copilotRepository.GetNextMessageSequenceAsync(conversationId);
        await _copilotRepository.AddMessageAsync(new CopilotMessage
        {
            ConversationId = conversationId,
            Role = "User",
            Content = request.Prompt,
            SequenceNo = replySequence
        });

        if (IsGreetingOrCapabilityQuery(request.Prompt))
        {
            const string intro = "Xin chào! Tôi là Copilot tuyển dụng của RecruitPro. Tôi có thể giúp bạn "
                + "xếp hạng ứng viên theo mức độ phù hợp, phân tích CV, giải thích lý do phù hợp/không phù hợp, "
                + "chuẩn bị câu hỏi phỏng vấn và chuyển hồ sơ sang Head Review. Hãy hỏi tôi về ứng viên hoặc "
                + "công việc bạn đang xem nhé.";
            await _copilotRepository.AddMessageAsync(new CopilotMessage
            {
                ConversationId = conversationId,
                Role = "Assistant",
                Content = intro,
                SequenceNo = replySequence + 1
            });
            conversation.UpdatedAt = DbDateTime.Now;
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<CopilotPromptResponseDto>.Ok(new CopilotPromptResponseDto
            {
                ConversationId = conversationId,
                DidRank = false,
                AssistantMessage = intro,
                NormalizedRules = new CopilotNormalizedRulesDto(),
                Results = []
            });
        }

        CopilotCandidatePoolDto? pool = await _copilotRepository.GetCandidatePoolAsync(request.JobId);
        if (pool is null)
        {
            return ApiResponse<CopilotPromptResponseDto>.NotFound(ErrorCodes.JobNotFound);
        }

        // Prior turns (this message isn't persisted-and-visible to itself yet, so this is exactly
        // "history before now") let the AI resolve short follow-ups like "gợi ý thêm" against what was
        // already discussed, instead of answering each message in isolation.
        CopilotConversation? withHistory = await _copilotRepository.GetConversationWithDetailsAsync(conversationId);
        IReadOnlyList<CopilotMessage> history = withHistory?.Messages
            .OrderBy(message => message.SequenceNo)
            .TakeLast(10)
            .ToList() ?? [];

        pool = await EnrichPoolWithResumeTextAsync(pool);
        string assistantReply = await _aiCopilotProvider.TryCreateChatReplyAsync(pool, request.Prompt, history, conversationId)
            ?? "Hiện chưa thể tạo phản hồi. Vui lòng thử lại hoặc chạy xếp hạng ứng viên để nhận đánh giá chi tiết.";

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

    // ---------------------------------------------------------------------------------------------
    // v2 shared ranking core. Ranking is the single source of truth for CV-screening evaluation:
    // search, fit-analysis and shortlist no longer run their own ranking pipeline (v2 §3).
    // ---------------------------------------------------------------------------------------------

    private sealed record CopilotRankingCoreResult(
        CopilotCandidatePoolDto Pool,
        CopilotNormalizedRulesDto Rules,
        IReadOnlyList<CopilotRankingResultDto> Results,
        CopilotAiMetadataDto Ai);

    /// <summary>
    /// Runs the deterministic ranking pipeline over the (already screening-filtered) pool: builds
    /// normalized rules once, ranks once, attaches Vietnamese fit-style evaluation, and optionally
    /// makes a single structured provider call to enrich the explanation. Deterministic ranking stays
    /// the source of truth; provider candidates are validated against the current screening pool.
    /// </summary>
    private async Task<CopilotRankingCoreResult> RunRankingCoreAsync(
        CopilotCandidatePoolDto screeningPool,
        Guid? callerUserId,
        string? prompt,
        IReadOnlyList<CopilotRuleCriterionRequestDto> priorityCriteria,
        IReadOnlyList<CopilotRuleCriterionRequestDto> negativeCriteria,
        IReadOnlyList<CopilotSavedRule> savedRules,
        CopilotNormalizedRulesDto? latestContextRules,
        Guid conversationId)
    {
        CopilotNormalizedRulesDto rules = BuildRules(
            prompt ?? string.Empty,
            screeningPool.Job.RequiredSkills,
            priorityCriteria,
            negativeCriteria,
            savedRules);

        if (latestContextRules is not null)
        {
            rules = MergeRules(latestContextRules, rules);
        }

        List<CopilotRankingResultDto> results = RankCandidates(screeningPool.Candidates, rules);
        ApplyVietnameseFitEvaluation(results, rules);

        CopilotAiMetadataDto ai = BuildDeterministicMetadata(
            "Xếp hạng sử dụng thuật toán chấm điểm tất định dựa trên bằng chứng hồ sơ; không bắt buộc gọi AI.");

        // At most one structured provider call per ranking run (v2 §13). Provider may ENRICH the
        // Vietnamese explanation (summary/evidence/strengths/gaps) only — deterministic ranking stays
        // the source of truth for candidate ORDER and scores. Provider candidates are validated
        // against the current screening pool (unknown ids ignored). On any failure the deterministic
        // ranking + Vietnamese fallback still stands.
        if (ShouldAttemptStructuredProvider() && screeningPool.Candidates.Count > 0)
        {
            CopilotPromptResponseDto? aiResponse = null;
            try
            {
                aiResponse = await _aiCopilotProvider.TryCreateRankingAsync(
                    screeningPool, rules, results, prompt ?? string.Empty, conversationId);
            }
            catch (Exception ex)
            {
                AddWarning(ai, $"Provider fallback: {ex.Message}");
            }

            if (aiResponse is not null && aiResponse.Results.Count > 0)
            {
                results = MergeProviderEnrichment(results, aiResponse, ref ai);
            }
            else if (aiResponse is null)
            {
                AddWarning(ai, "Provider fallback: no structured ranking returned.");
            }
        }

        return new CopilotRankingCoreResult(screeningPool, rules, results, ai);
    }

    /// <summary>
    /// Merges provider enrichment into the deterministic ranking. By default deterministic ORDER and
    /// scores are preserved and only Vietnamese prose (summary/evidence/strengths/gaps) is merged for
    /// candidates the provider references AND that exist in the screening pool. Provider reordering is
    /// applied only when <see cref="AiProviderSettings.AllowProviderReordering"/> is true; otherwise a
    /// differing provider order is ignored with a "provider-order:ignored" warning. Unknown provider
    /// candidate ids are ignored with a "provider-candidate:unknown" warning.
    /// </summary>
    private List<CopilotRankingResultDto> MergeProviderEnrichment(
        List<CopilotRankingResultDto> deterministic,
        CopilotPromptResponseDto aiResponse,
        ref CopilotAiMetadataDto ai)
    {
        List<string> warnings = ["provider-json:valid"];

        HashSet<Guid> knownCandidateIds = deterministic.Select(result => result.CandidateUserId).ToHashSet();
        List<CopilotRankingResultDto> providerResults = aiResponse.Results.ToList();
        List<CopilotRankingResultDto> knownProvider = providerResults
            .Where(provider => knownCandidateIds.Contains(provider.CandidateUserId))
            .ToList();

        if (knownProvider.Count != providerResults.Count)
        {
            // Provider tried to introduce candidates/applications outside the screening pool.
            warnings.Add("provider-candidate:unknown");
        }

        // Merge Vietnamese prose per validated candidate. Machine values (scores, rank position,
        // recommendation, fit label, confidence) always stay deterministic.
        Dictionary<Guid, CopilotRankingResultDto> providerByCandidate = knownProvider
            .GroupBy(provider => provider.CandidateUserId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (CopilotRankingResultDto result in deterministic)
        {
            if (!providerByCandidate.TryGetValue(result.CandidateUserId, out CopilotRankingResultDto? provider))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(provider.Summary))
            {
                result.Summary = provider.Summary.Trim();
                result.IsAiGenerated = true;
            }
            if (provider.Strengths.Count > 0) result.Strengths = provider.Strengths;
            if (provider.Weaknesses.Count > 0) result.Weaknesses = provider.Weaknesses;
            if (provider.Evidence.Count > 0) result.Evidence = provider.Evidence;
        }

        // Ordering decision — deterministic by default.
        List<Guid> deterministicOrder = deterministic.Select(result => result.CandidateUserId).ToList();
        List<Guid> providerOrder = knownProvider.Select(provider => provider.CandidateUserId).Distinct().ToList();
        bool orderDiffers = !providerOrder.SequenceEqual(deterministicOrder.Where(providerOrder.Contains).ToList());

        List<CopilotRankingResultDto> ordered = deterministic;
        if (_aiProviderSettings.AllowProviderReordering && orderDiffers && providerOrder.Count > 0)
        {
            Dictionary<Guid, int> providerRank = providerOrder
                .Select((id, index) => (id, index))
                .ToDictionary(pair => pair.id, pair => pair.index);
            ordered = deterministic
                .OrderBy(result => result.IsAutoRejected)
                .ThenBy(result => providerRank.TryGetValue(result.CandidateUserId, out int idx) ? idx : int.MaxValue)
                .ThenBy(result => result.RankPosition)
                .ToList();
            for (int i = 0; i < ordered.Count; i += 1)
            {
                ordered[i].RankPosition = i + 1;
            }
            warnings.Add("provider-order:applied");
        }
        else if (orderDiffers)
        {
            warnings.Add("provider-order:ignored");
        }

        ai = new CopilotAiMetadataDto
        {
            AuditId = Guid.NewGuid(),
            FallbackUsed = false,
            ProviderName = "configured-ai-provider",
            ModelName = _aiProviderSettings.Model,
            Warnings = warnings
        };

        return ordered;
    }

    /// <summary>
    /// v2 §6 — restricts the candidate pool to applications currently in the CV-screening stage.
    /// Everything already at Head Review (ManagerReview), Interview, Offer, Hired, Rejected,
    /// OfferDeclined or Withdrawn is excluded from default ranking.
    /// </summary>
    private static CopilotCandidatePoolDto FilterToScreeningPool(CopilotCandidatePoolDto pool)
    {
        return new CopilotCandidatePoolDto
        {
            Job = pool.Job,
            Candidates = pool.Candidates
                .Where(candidate => string.Equals(candidate.Status, ScreeningStatus, StringComparison.OrdinalIgnoreCase))
                .ToList()
        };
    }

    /// <summary>
    /// v2 §8 — stable SHA-256 fingerprint of the effective ranking input. Any change to the job,
    /// prompt, criteria, active saved rules, latest-context flag, or the screening pool's candidates
    /// and their ranking-relevant evidence produces a different hash and forces a fresh ranking run.
    /// </summary>
    private static string ComputeRankingInputHash(
        Guid jobId,
        Guid userId,
        CopilotNormalizedRulesDto effectiveRules,
        CopilotCandidatePoolDto screeningPool)
    {
        var payload = new
        {
            jobId,
            userId,
            // The effective merged rules already encode prompt + criteria + saved rules + latest
            // context, so hashing them keeps the fingerprint stable across identical re-clicks.
            effectiveRules = JsonSerializer.Serialize(effectiveRules, JsonOptions),
            requiredSkills = screeningPool.Job.RequiredSkills.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            candidates = screeningPool.Candidates
                .OrderBy(candidate => candidate.ApplicationId)
                .Select(candidate => new
                {
                    candidate.ApplicationId,
                    candidate.CandidateUserId,
                    candidate.Status,
                    candidate.ExperienceYears,
                    education = candidate.Education ?? string.Empty,
                    skills = candidate.Skills.OrderBy(s => s, StringComparer.Ordinal).ToList(),
                    cvSummary = candidate.CvSummary ?? string.Empty
                })
                .ToList()
        };

        string serialized = JsonSerializer.Serialize(payload, JsonOptions);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// A bare greeting or "what can you do" has no recruitment content for the AI to reason about, so
    /// it's handled here with a canned capabilities intro instead of a wasted AI call. Actual scope
    /// judgment for everything else happens in the AI provider's system prompt (v2 §14) — see
    /// <see cref="IAiCopilotProvider.TryCreateChatReplyAsync"/>.
    /// </summary>
    private static bool IsGreetingOrCapabilityQuery(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return false;
        }

        string lowered = prompt.Trim().ToLowerInvariant();
        string[] greetingPhrases =
        [
            "hi", "hello", "hey", "alo", "chào", "xin chào", "helo",
            "bạn là ai", "bạn có thể làm gì", "bạn làm được gì", "bạn giúp được gì",
            "làm được những gì", "giúp gì được", "what can you do", "who are you"
        ];

        return greetingPhrases.Any(phrase => lowered == phrase || lowered.Contains(phrase));
    }

    /// <summary>
    /// Attaches Vietnamese fit-style evaluation (fit label, confidence, evidence and a detailed
    /// Vietnamese summary) to each ranking result. Machine-readable values (StrongFit/PotentialFit/
    /// RiskFit/NotRecommended, Interview/Consider/Hold/Reject) are kept unchanged.
    /// </summary>
    private static void ApplyVietnameseFitEvaluation(
        IReadOnlyList<CopilotRankingResultDto> results,
        CopilotNormalizedRulesDto rules,
        bool preserveSummary = false)
    {
        foreach (CopilotRankingResultDto result in results)
        {
            string fitLabel = ResolveFitLabel(result);
            result.FitLabel = fitLabel;
            result.ConfidenceScore = result.IsAutoRejected
                ? Math.Min(40, Math.Max(20, result.TotalScore))
                : Math.Min(100, Math.Max(35, result.TotalScore));
            result.Evidence = BuildVietnameseEvidence(result, rules);

            if (!preserveSummary || string.IsNullOrWhiteSpace(result.Summary))
            {
                result.Summary = BuildVietnameseFitSummary(result, fitLabel);
            }
        }
    }

    private static string ResolveFitLabel(CopilotRankingResultDto result)
    {
        if (result.IsAutoRejected)
        {
            return "NotRecommended";
        }

        return result.TotalScore >= 80
            ? "StrongFit"
            : result.TotalScore >= 60
                ? "PotentialFit"
                : "RiskFit";
    }

    private static IReadOnlyList<string> BuildVietnameseEvidence(CopilotRankingResultDto result, CopilotNormalizedRulesDto rules)
    {
        List<string> evidence = [];

        if (result.Strengths.Count > 0)
        {
            evidence.Add($"Điểm mạnh: {string.Join(", ", result.Strengths.Take(4))}.");
        }

        if (result.Weaknesses.Count > 0)
        {
            evidence.Add($"Khoảng trống cần lưu ý: {string.Join(", ", result.Weaknesses.Take(3))}.");
        }

        if (rules.MinExperienceYears.HasValue)
        {
            evidence.Add($"Kỳ vọng kinh nghiệm tối thiểu {rules.MinExperienceYears.Value} năm.");
        }

        evidence.Add($"Điểm tổng {result.TotalScore:0.#}/100 (kỹ năng {result.SkillScore:0.#}, kinh nghiệm {result.ExperienceScore:0.#}, học vấn {result.EducationScore:0.#}).");

        if (result.IsAutoRejected && !string.IsNullOrWhiteSpace(result.RejectReason))
        {
            evidence.Add($"Lý do tự động loại: {result.RejectReason}.");
        }

        return evidence;
    }

    private static string BuildVietnameseFitSummary(CopilotRankingResultDto result, string fitLabel)
    {
        if (result.IsAutoRejected)
        {
            string reason = string.IsNullOrWhiteSpace(result.RejectReason)
                ? "khớp một tiêu chí loại trừ đang bật"
                : result.RejectReason;
            return $"Ứng viên {result.FullName} không được đề xuất theo tiêu chí hiện tại vì {reason}. "
                + "Nên giữ ở vòng CV screening và không chuyển tiếp.";
        }

        string strengths = result.Strengths.Count > 0
            ? string.Join(", ", result.Strengths.Take(4))
            : "các bằng chứng hiện có trong hồ sơ";
        string gaps = result.Weaknesses.Count > 0
            ? $" Điểm cần xác minh thêm ở vòng Head Review: {string.Join(", ", result.Weaknesses.Take(3))}."
            : string.Empty;

        string fitPhrase = fitLabel switch
        {
            "StrongFit" => "được đánh giá là StrongFit — phù hợp mạnh với vị trí",
            "PotentialFit" => "được đánh giá là PotentialFit — có tiềm năng phù hợp",
            _ => "được đánh giá là RiskFit — còn nhiều rủi ro về độ phù hợp"
        };

        string recommendation = result.Recommendation switch
        {
            "Interview" => " Nên ưu tiên chuyển sang vòng Head Review.",
            "Consider" => " Có thể cân nhắc chuyển sang vòng Head Review sau khi rà soát thêm.",
            _ => " Nên rà soát kỹ trước khi quyết định chuyển tiếp.",
        };

        return $"Ứng viên {result.FullName} {fitPhrase} với điểm tổng {result.TotalScore:0.#}/100, "
            + $"dựa trên {strengths}.{gaps}{recommendation}";
    }

    /// <summary>
    /// Persists a <see cref="CandidateFitAnalysis"/> snapshot per ranking result so the candidate
    /// review-detail screen and the read-only fit endpoint can read fit data created during ranking.
    /// </summary>
    private async Task PersistFitSnapshotsAsync(
        Guid jobId,
        Guid auditId,
        CopilotAiMetadataDto ai,
        IReadOnlyList<CopilotRankingResultDto> results)
    {
        if (results.Count == 0)
        {
            return;
        }

        foreach (CopilotRankingResultDto result in results)
        {
            await _copilotRepository.AddFitAnalysisAsync(new CandidateFitAnalysis
            {
                AuditId = auditId,
                JobId = jobId,
                CandidateUserId = result.CandidateUserId,
                ApplicationId = result.ApplicationId,
                FitLabel = string.IsNullOrWhiteSpace(result.FitLabel) ? ResolveFitLabel(result) : result.FitLabel,
                ConfidenceScore = result.ConfidenceScore,
                TotalScore = result.TotalScore,
                StrengthsJson = JsonSerializer.Serialize(result.Strengths),
                GapsJson = JsonSerializer.Serialize(result.Weaknesses),
                EvidenceJson = JsonSerializer.Serialize(result.Evidence),
                Summary = result.Summary,
                ProviderName = ai.ProviderName,
                ModelName = ai.ModelName,
                FallbackUsed = ai.FallbackUsed
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// v2 §7 — explicit HR action to pass selected ranked candidates from CV screening to Head Review
    /// (Screening -> ManagerReview). AI never performs this transition; it only recommends. Each
    /// selected application is validated (belongs to this ranking session's job, currently in
    /// Screening) and moved by reusing the existing status-transition service so all workflow rules,
    /// the DepartmentHeadReviewRequestedAt timestamp and the Head Review notification are honored.
    /// </summary>
    public async Task<ApiResponse<PassCvResultDto>> PassCvToHeadReviewAsync(
        Guid rankingSessionId,
        PassCvToHeadReviewRequest request,
        Guid userId,
        IReadOnlyCollection<string> callerRoles)
    {
        CopilotRankingSession? session = await _copilotRepository.GetRankingSessionAsync(rankingSessionId);
        if (session is null || session.UserId != userId)
        {
            return ApiResponse<PassCvResultDto>.NotFound(ErrorCodes.EntityNotFound);
        }

        List<Guid> requested = request.ApplicationIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (requested.Count == 0)
        {
            return ApiResponse<PassCvResultDto>.BadRequest(ErrorCodes.InvalidInput);
        }

        HashSet<Guid> sessionApplicationIds = session.Results.Select(result => result.ApplicationId).ToHashSet();
        List<PassCvUpdatedDto> updated = [];
        List<PassCvSkippedDto> skipped = [];

        foreach (Guid applicationId in requested)
        {
            if (!sessionApplicationIds.Contains(applicationId))
            {
                skipped.Add(new PassCvSkippedDto { ApplicationId = applicationId, Reason = "Hồ sơ không thuộc phiên xếp hạng hiện tại." });
                continue;
            }

            RecruitPro.Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(applicationId);
            if (application is null)
            {
                skipped.Add(new PassCvSkippedDto { ApplicationId = applicationId, Reason = "Không tìm thấy hồ sơ ứng tuyển." });
                continue;
            }

            if (application.JobId != session.JobId)
            {
                skipped.Add(new PassCvSkippedDto { ApplicationId = applicationId, Reason = "Hồ sơ không thuộc vị trí của phiên xếp hạng này." });
                continue;
            }

            if (application.Status != ApplicationStatus.Screening)
            {
                skipped.Add(new PassCvSkippedDto
                {
                    ApplicationId = applicationId,
                    Reason = $"Hồ sơ không ở trạng thái Screening (hiện tại: {application.Status})."
                });
                continue;
            }

            ApiResponse<ApplicationReviewDetailDto> transition = await _applicationService.UpdateApplicationDecisionAsync(
                applicationId.ToString(),
                userId,
                new UpdateApplicationDecisionRequest { TargetStatus = ApplicationStatus.ManagerReview.ToString() });

            if (transition.Success)
            {
                updated.Add(new PassCvUpdatedDto
                {
                    ApplicationId = applicationId,
                    OldStatus = ApplicationStatus.Screening.ToString(),
                    NewStatus = ApplicationStatus.ManagerReview.ToString()
                });
            }
            else
            {
                skipped.Add(new PassCvSkippedDto
                {
                    ApplicationId = applicationId,
                    Reason = string.IsNullOrWhiteSpace(transition.Message)
                        ? "Không thể chuyển hồ sơ sang Head Review."
                        : transition.Message
                });
            }
        }

        return ApiResponse<PassCvResultDto>.Ok(new PassCvResultDto { Updated = updated, Skipped = skipped });
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
            return ApiResponse<CopilotRankingSessionDetailDto>.NotFound(ErrorCodes.EntityNotFound);
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
            return ApiResponse<CopilotSavedRuleDto>.NotFound(ErrorCodes.EntityNotFound);
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
            return ApiResponse<object>.NotFound(ErrorCodes.EntityNotFound);
        }

        rule.IsDeleted = true;
        rule.IsActive = false;
        rule.DeletedAt = DbDateTime.Now;
        rule.UpdatedAt = DbDateTime.Now;
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<object>.Ok(new { ruleId });
    }

    private async Task PersistArtifactAsync(
        Guid? ownerUserId,
        Guid? jobId,
        Guid? applicationId,
        string artifactType,
        string? prompt,
        object payload,
        CopilotAiMetadataDto ai)
    {
        if (!ownerUserId.HasValue)
        {
            return;
        }

        Guid artifactId = ai.ArtifactId ?? ai.AuditId;
        ai.ArtifactId = artifactId;

        await _copilotRepository.AddGeneratedArtifactAsync(new CopilotGeneratedArtifact
        {
            Id = artifactId,
            OwnerUserId = ownerUserId.Value,
            JobId = jobId,
            ApplicationId = applicationId,
            ArtifactType = artifactType,
            Prompt = string.IsNullOrWhiteSpace(prompt) ? string.Empty : prompt.Trim(),
            PayloadJson = JsonSerializer.Serialize(payload),
            ProviderName = ai.ProviderName,
            ModelName = ai.ModelName,
            FallbackUsed = ai.FallbackUsed
        });
        await _unitOfWork.SaveChangesAsync();
    }

    private static CopilotPromptTemplateDto MapPromptTemplate(CopilotPromptTemplate template)
    {
        return new CopilotPromptTemplateDto
        {
            TemplateId = template.Id,
            Name = template.Name,
            TemplateType = template.TemplateType,
            Prompt = template.Prompt,
            IsActive = template.IsActive,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    private async Task<T> TryGenerateStructuredResponseAsync<T>(
        string actionType,
        string templateType,
        Guid? callerUserId,
        string defaultInstructions,
        IReadOnlyDictionary<string, string> templateVariables,
        object payload,
        T fallback,
        Func<T, bool> validate,
        Action<T> normalize,
        Func<T, CopilotAiMetadataDto> getMetadata,
        Action<T, CopilotAiMetadataDto> setMetadata)
    {
        if (!ShouldAttemptStructuredProvider())
        {
            return fallback;
        }

        CopilotPromptTemplate? template = await ResolvePromptTemplateAsync(callerUserId, templateType);
        string userPrompt = BuildStructuredPrompt(template, defaultInstructions, templateVariables, payload);

        AiStructuredJsonResult providerResult;
        try
        {
            providerResult = await _aiCopilotProvider.TryCreateStructuredJsonAsync(
                actionType,
                "You are RecruitPro's AI Recruitment Copilot. Return valid JSON only matching the exact requested shape. "
                + "Use Vietnamese for all human-facing prose fields (summaries, questions, evidence, rationale). "
                + "Keep JSON keys and machine-readable enum/code values unchanged. "
                + "Ground outputs in the provided ATS context and never mutate ATS state.",
                userPrompt);
        }
        catch (Exception ex)
        {
            AddWarning(getMetadata(fallback), $"Provider fallback: {ex.Message}");
            return fallback;
        }

        if (!providerResult.Succeeded || string.IsNullOrWhiteSpace(providerResult.Json))
        {
            AddWarning(getMetadata(fallback), $"Provider fallback: {providerResult.FailureReason ?? "no structured JSON"}");
            return fallback;
        }

        T? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<T>(providerResult.Json, JsonOptions);
        }
        catch (JsonException ex)
        {
            AddWarning(getMetadata(fallback), $"Provider JSON parse failed: {ex.Message}");
            return fallback;
        }

        if (parsed is null)
        {
            AddWarning(getMetadata(fallback), "Provider JSON parse failed: empty payload.");
            return fallback;
        }

        normalize(parsed);
        if (!validate(parsed))
        {
            AddWarning(getMetadata(fallback), "Provider JSON validation failed: missing required fields.");
            return fallback;
        }

        CopilotAiMetadataDto providerMetadata = BuildProviderMetadata(providerResult, template);
        setMetadata(parsed, providerMetadata);
        return parsed;
    }

    private bool ShouldAttemptStructuredProvider()
        => _aiProviderSettings.Enabled && !string.IsNullOrWhiteSpace(_aiProviderSettings.ApiKey);

    private async Task<CopilotPromptTemplate?> ResolvePromptTemplateAsync(Guid? ownerUserId, string templateType)
    {
        if (!ownerUserId.HasValue)
        {
            return null;
        }

        IReadOnlyList<CopilotPromptTemplate> templates = await _copilotRepository.GetPromptTemplatesAsync(ownerUserId.Value);
        return templates
            .Where(template => template.IsActive)
            .FirstOrDefault(template => string.Equals(template.TemplateType, templateType, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildStructuredPrompt(
        CopilotPromptTemplate? template,
        string defaultInstructions,
        IReadOnlyDictionary<string, string> variables,
        object payload)
    {
        string templateText = template?.Prompt ?? string.Empty;
        foreach ((string key, string value) in variables)
        {
            templateText = templateText
                .Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase)
                .Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        string templateSection = string.IsNullOrWhiteSpace(templateText)
            ? "No saved prompt template was found. Use the default task instructions below."
            : $"Saved prompt template ({template!.Name}/{template.TemplateType}):\n{templateText.Trim()}";

        return $$"""
        {{templateSection}}

        Task instructions and required JSON shape:
        {{defaultInstructions}}

        ATS context JSON:
        {{JsonSerializer.Serialize(payload, JsonOptions)}}
        """;
    }

    private static CopilotAiMetadataDto BuildProviderMetadata(
        AiStructuredJsonResult result,
        CopilotPromptTemplate? template)
    {
        List<string> warnings = ["provider-json:valid"];
        if (template is not null)
        {
            warnings.Add($"prompt-template:{template.Name}");
        }

        return new CopilotAiMetadataDto
        {
            AuditId = Guid.NewGuid(),
            FallbackUsed = false,
            ProviderName = string.IsNullOrWhiteSpace(result.ProviderName) ? "configured-ai-provider" : result.ProviderName,
            ModelName = result.ModelName,
            Warnings = warnings
        };
    }

    private static void AddWarning(CopilotAiMetadataDto metadata, string warning)
    {
        metadata.Warnings = metadata.Warnings.Concat([warning]).ToList();
    }

    private static bool ValidateSearchResponse(NaturalLanguageCandidateSearchResponseDto response)
    {
        return response.Results.Count > 0
            && response.Results.All(result =>
                result.CandidateUserId != Guid.Empty
                && result.ApplicationId != Guid.Empty
                && !string.IsNullOrWhiteSpace(result.FullName)
                && !string.IsNullOrWhiteSpace(result.Evidence));
    }

    private static bool ValidateFitAnalysisResponse(CandidateFitAnalysisResponseDto response)
    {
        return response.JobId != Guid.Empty
            && response.Analyses.Count > 0
            && response.Analyses.All(analysis =>
                analysis.CandidateUserId != Guid.Empty
                && analysis.ApplicationId != Guid.Empty
                && !string.IsNullOrWhiteSpace(analysis.FullName)
                && !string.IsNullOrWhiteSpace(analysis.FitLabel)
                && !string.IsNullOrWhiteSpace(analysis.Summary));
    }

    private static bool ValidateQuestionSet(InterviewQuestionSetDto response)
    {
        return response.JobId != Guid.Empty
            && response.Questions.Count > 0
            && response.Questions.All(question =>
                !string.IsNullOrWhiteSpace(question.Category)
                && !string.IsNullOrWhiteSpace(question.Question)
                && !string.IsNullOrWhiteSpace(question.Evidence));
    }

    private static bool ValidateEmailDraft(HrEmailDraftResponseDto response)
    {
        return response.ApplicationId != Guid.Empty
            && !string.IsNullOrWhiteSpace(response.TemplateType)
            && !string.IsNullOrWhiteSpace(response.Subject)
            && !string.IsNullOrWhiteSpace(response.Body);
    }

    private static string SearchInstructions(int maxResults)
    {
        return $$"""
        Generate natural-language candidate search output using only the provided candidates.
        Return this JSON shape:
        {
          "normalizedIntent": "candidate_search",
          "query": "string",
          "extractedFilters": { "requiredSkills": [], "preferredSkills": [], "minExperienceYears": null, "autoRejectRules": [], "minTotalScore": null, "priorityCriteria": [], "negativeCriteria": [] },
          "results": [
            { "candidateUserId": "uuid", "applicationId": "uuid", "fullName": "name", "matchScore": 0, "matchedSkills": [], "missingSkills": [], "evidence": "ATS evidence sentence" }
          ]
        }
        Required: query, extractedFilters, 1..{{maxResults}} results, candidate ids, application ids, fullName, evidence.
        """;
    }

    private static string FitAnalysisInstructions()
    {
        return """
        Generate candidate fit analysis output using only the provided ATS evidence.
        Return this JSON shape:
        {
          "jobId": "uuid",
          "analyses": [
            { "candidateUserId": "uuid", "applicationId": "uuid", "fullName": "name", "fitLabel": "StrongFit|PotentialFit|RiskFit|NotRecommended", "confidenceScore": 0, "totalScore": 0, "strengths": [], "gaps": [], "evidence": [], "summary": "short grounded summary" }
          ]
        }
        Required: jobId, at least one analysis, candidate/application ids, fullName, fitLabel, summary.
        """;
    }

    private static string QuestionInstructions(int questionCount)
    {
        return $$"""
        Generate an interview question set using only the provided job/candidate evidence.
        Return this JSON shape:
        {
          "jobId": "uuid",
          "candidateUserId": "uuid or null",
          "focus": "string",
          "questions": [
            { "category": "string", "question": "string", "evidence": "string" }
          ]
        }
        Required: jobId, focus, exactly or up to {{questionCount}} useful questions, category, question, evidence.
        Use Vietnamese for all human-facing prose (question, evidence, category). Keep JSON keys unchanged.
        """;
    }

    private static string EmailInstructions()
    {
        return """
        Generate an HR email draft. Do not send the email.
        Return this JSON shape:
        {
          "applicationId": "uuid",
          "templateType": "string",
          "subject": "string",
          "body": "string",
          "evidence": []
        }
        Required: applicationId, templateType, subject, body.
        """;
    }

    private static CandidateFitAnalysisSnapshotDto MapFitAnalysisSnapshot(CandidateFitAnalysis analysis)
    {
        return new CandidateFitAnalysisSnapshotDto
        {
            FitAnalysisId = analysis.Id,
            AuditId = analysis.AuditId,
            JobId = analysis.JobId,
            CandidateUserId = analysis.CandidateUserId,
            ApplicationId = analysis.ApplicationId,
            FullName = analysis.CandidateUser?.FullName ?? string.Empty,
            FitLabel = analysis.FitLabel,
            ConfidenceScore = analysis.ConfidenceScore,
            TotalScore = analysis.TotalScore,
            Strengths = DeserializeStringList(analysis.StrengthsJson),
            Gaps = DeserializeStringList(analysis.GapsJson),
            Evidence = DeserializeStringList(analysis.EvidenceJson),
            Summary = analysis.Summary,
            ProviderName = analysis.ProviderName,
            ModelName = analysis.ModelName,
            FallbackUsed = analysis.FallbackUsed,
            CreatedAt = analysis.CreatedAt
        };
    }

    private static CopilotGeneratedArtifactDto MapGeneratedArtifact(CopilotGeneratedArtifact artifact)
    {
        return new CopilotGeneratedArtifactDto
        {
            ArtifactId = artifact.Id,
            OwnerUserId = artifact.OwnerUserId,
            JobId = artifact.JobId,
            ApplicationId = artifact.ApplicationId,
            ArtifactType = artifact.ArtifactType,
            Prompt = artifact.Prompt,
            PayloadJson = artifact.PayloadJson,
            ProviderName = artifact.ProviderName,
            ModelName = artifact.ModelName,
            FallbackUsed = artifact.FallbackUsed,
            CreatedAt = artifact.CreatedAt
        };
    }

    private static ApiResponse<T> MirrorFailure<T>(int statusCode, string message, string? errorCode = null)
    {
        // Code-first: mirror the upstream failure by its stable code, not its human message. The
        // message param is kept so callers still pass (status, message, code); message resolution
        // now happens centrally from the code via IErrorMessageProvider.
        return statusCode switch
        {
            400 => ApiResponse<T>.BadRequest(errorCode ?? ErrorCodes.InvalidInput),
            403 => ApiResponse<T>.Forbidden(errorCode ?? ErrorCodes.Forbidden),
            404 => ApiResponse<T>.NotFound(errorCode ?? ErrorCodes.EntityNotFound),
            422 => ApiResponse<T>.UnprocessableEntity(errorCode ?? ErrorCodes.BusinessRuleViolation),
            _ => ApiResponse<T>.Error(errorCode ?? ErrorCodes.ServerError)
        };
    }

    private static CopilotAiMetadataDto BuildDeterministicMetadata(string warning)
    {
        return new CopilotAiMetadataDto
        {
            AuditId = Guid.NewGuid(),
            FallbackUsed = true,
            ProviderName = "deterministic-copilot",
            ModelName = "deterministic-copilot-v2",
            Warnings = [warning]
        };
    }

    private static CopilotCandidateSearchResultDto BuildSearchResult(
        CopilotRankingResultDto result,
        CopilotNormalizedRulesDto rules)
    {
        List<string> missingSkills = rules.RequiredSkills
            .Except(result.Strengths, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CopilotCandidateSearchResultDto
        {
            CandidateUserId = result.CandidateUserId,
            ApplicationId = result.ApplicationId,
            FullName = result.FullName,
            MatchScore = result.TotalScore,
            MatchedSkills = result.Strengths,
            MissingSkills = missingSkills,
            Evidence = BuildEvidenceSentence(result)
        };
    }

    private static CandidateFitAnalysisDto BuildFitAnalysis(CopilotRankingResultDto result)
    {
        // The ranking result already carries the Vietnamese fit-style evaluation (v2 §4). Reuse it
        // so fit-analysis stays consistent with ranking and does not re-derive anything.
        string fitLabel = string.IsNullOrWhiteSpace(result.FitLabel) ? ResolveFitLabel(result) : result.FitLabel;
        IReadOnlyList<string> evidence = result.Evidence.Count > 0 ? result.Evidence : BuildRationale(result);
        string summary = string.IsNullOrWhiteSpace(result.Summary) ? BuildVietnameseFitSummary(result, fitLabel) : result.Summary;

        return new CandidateFitAnalysisDto
        {
            CandidateUserId = result.CandidateUserId,
            ApplicationId = result.ApplicationId,
            FullName = result.FullName,
            FitLabel = fitLabel,
            ConfidenceScore = result.ConfidenceScore > 0 ? result.ConfidenceScore : Math.Min(100, Math.Max(35, result.TotalScore)),
            TotalScore = result.TotalScore,
            Strengths = result.Strengths,
            Gaps = result.Weaknesses,
            Evidence = evidence,
            Summary = summary
        };
    }

    private static IReadOnlyList<string> BuildRationale(CopilotRankingResultDto result)
    {
        List<string> rationale = [];
        if (result.Strengths.Count > 0)
        {
            rationale.Add($"Strengths: {string.Join(", ", result.Strengths.Take(3))}");
        }
        if (result.Weaknesses.Count > 0)
        {
            rationale.Add($"Gaps: {string.Join(", ", result.Weaknesses.Take(3))}");
        }
        rationale.Add($"Scores: total {result.TotalScore:0.#}, skills {result.SkillScore:0.#}, experience {result.ExperienceScore:0.#}");
        if (result.IsAutoRejected && !string.IsNullOrWhiteSpace(result.RejectReason))
        {
            rationale.Add($"Auto-reject reason: {result.RejectReason}");
        }

        return rationale;
    }

    private static string BuildEvidenceSentence(CopilotRankingResultDto result)
    {
        string strengths = result.Strengths.Count == 0
            ? "no explicit required skill match"
            : string.Join(", ", result.Strengths.Take(3));
        string gaps = result.Weaknesses.Count == 0
            ? "no major deterministic gap"
            : string.Join(", ", result.Weaknesses.Take(2));

        return $"{strengths}; {gaps}.";
    }

    private static string BuildFitSummary(CopilotRankingResultDto result, string fitLabel)
    {
        if (result.IsAutoRejected)
        {
            return $"{result.FullName} is not recommended by the active deterministic criteria: {result.RejectReason ?? "auto-reject rule matched"}.";
        }

        string strengths = result.Strengths.Count > 0
            ? string.Join(", ", result.Strengths.Take(3))
            : "the available profile evidence";
        string gaps = result.Weaknesses.Count > 0
            ? $" Main gaps: {string.Join(", ", result.Weaknesses.Take(2))}."
            : string.Empty;

        return $"{result.FullName} is a {fitLabel} with score {result.TotalScore:0.#}, supported by {strengths}.{gaps}";
    }

    private static CopilotCandidateDto? ResolveCandidate(
        CopilotCandidatePoolDto pool,
        Guid? candidateUserId,
        Guid? applicationId)
    {
        if (candidateUserId.HasValue && candidateUserId.Value != Guid.Empty)
        {
            return pool.Candidates.FirstOrDefault(candidate => candidate.CandidateUserId == candidateUserId.Value);
        }

        if (applicationId.HasValue && applicationId.Value != Guid.Empty)
        {
            return pool.Candidates.FirstOrDefault(candidate => candidate.ApplicationId == applicationId.Value);
        }

        return null;
    }

    private static IReadOnlyList<InterviewQuestionDto> BuildInterviewQuestions(
        CopilotJobContextDto job,
        CopilotCandidateDto? candidate,
        string focus)
    {
        List<InterviewQuestionDto> questions = [];
        IReadOnlyList<string> skills = job.RequiredSkills.Count > 0
            ? job.RequiredSkills
            : job.Requirements.Take(4).ToList();

        foreach (string skill in skills.Take(5))
        {
            questions.Add(new InterviewQuestionDto
            {
                Category = "Kỹ thuật",
                Question = $"Hãy mô tả một dự án gần đây bạn sử dụng {skill}. Bạn đã đánh đổi những gì khi ra quyết định?",
                Evidence = $"Vị trí yêu cầu {skill}."
            });
        }

        questions.Add(new InterviewQuestionDto
        {
            Category = "Độ phù hợp vai trò",
            Question = $"Theo bạn, phần khó nhất của vị trí {job.Title} trong 60 ngày đầu là gì?",
            Evidence = $"Bối cảnh vị trí: {job.Title}."
        });

        questions.Add(new InterviewQuestionDto
        {
            Category = "Giải quyết vấn đề",
            Question = "Hãy kể về một sự cố production bạn đã điều tra từ triệu chứng đến nguyên nhân gốc. Bạn đo lường điều gì đầu tiên?",
            Evidence = "Tín hiệu chung về năng lực gỡ lỗi có cấu trúc."
        });

        if (candidate is not null)
        {
            string candidateSkills = candidate.Skills.Count > 0
                ? string.Join(", ", candidate.Skills.Take(4))
                : "kinh nghiệm đã liệt kê";
            questions.Add(new InterviewQuestionDto
            {
                Category = "Bằng chứng ứng viên",
                Question = $"Hồ sơ của bạn nổi bật ở {candidateSkills}. Đâu là điểm chứng minh rõ nhất sự sẵn sàng cho vị trí này, và vì sao?",
                Evidence = $"Hồ sơ ứng viên: {candidateSkills}."
            });
        }

        if (!string.Equals(focus, "job-fit", StringComparison.OrdinalIgnoreCase))
        {
            questions.Add(new InterviewQuestionDto
            {
                Category = "Trọng tâm tùy chỉnh",
                Question = $"Với trọng tâm \"{focus}\", chúng ta nên tìm bằng chứng cụ thể nào trong buổi phỏng vấn này?",
                Evidence = "Trọng tâm phỏng vấn do nhà tuyển dụng cung cấp."
            });
        }

        return questions;
    }

    private static (string Subject, string Body) BuildEmailDraft(
        string candidateName,
        string jobTitle,
        string templateType,
        string tone,
        string? additionalInstruction)
    {
        string subject = templateType.ToLowerInvariant() switch
        {
            "interview_invite" => $"Interview next step for {jobTitle}",
            "offer_follow_up" => $"Following up on your {jobTitle} offer",
            "rejection" => $"Update on your {jobTitle} application",
            _ => $"Update on your {jobTitle} application"
        };

        string opener = tone.Contains("warm", StringComparison.OrdinalIgnoreCase)
            ? $"Hi {candidateName},\n\nThank you again for the time and care you put into your application."
            : $"Hi {candidateName},\n\nThank you for your interest in the {jobTitle} role.";

        string body = templateType.ToLowerInvariant() switch
        {
            "interview_invite" => $"{opener}\n\nWe would like to invite you to the next interview step for {jobTitle}. Please share a few time windows that work for you, and our team will confirm the schedule.\n\nBest regards,\nRecruitPro HR",
            "offer_follow_up" => $"{opener}\n\nWe are following up on the offer for {jobTitle}. Please let us know if you have any questions about the role, compensation, or next steps.\n\nBest regards,\nRecruitPro HR",
            "rejection" => $"{opener}\n\nAfter reviewing the current hiring needs, we will not be moving forward with your application for this role. We appreciate your interest and encourage you to apply again when a suitable opening appears.\n\nBest regards,\nRecruitPro HR",
            _ => $"{opener}\n\nYour application is currently under review. We will keep you updated as soon as there is a confirmed next step.\n\nBest regards,\nRecruitPro HR"
        };

        if (!string.IsNullOrWhiteSpace(additionalInstruction))
        {
            body += $"\n\nRecruiter note to review before sending: {additionalInstruction.Trim()}";
        }

        return (subject, body);
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
            decimal projectScore = Math.Min(10, (cvMatchedSkills.Count * 2) + (cvMatchedPreferredSkills.Count) + Math.Min(4, (candidate.CvSummary?.Length ?? 0) / 120m));
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
                    : result.RejectReason ?? "Không đáp ứng các tiêu chí hiện tại.";
                string prefix = result.IsAutoRejected
                    ? $"Đã loại - {result.FullName}:"
                    : $"{result.RankPosition}. {result.FullName}:";
                return $"{prefix} {reason}";
            })
            .ToList();

        return lines.Count > 0
            ? string.Join("\n", lines)
            : "Đã hoàn tất xếp hạng ứng viên trong vòng CV screening.";
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
