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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ICopilotRepository _copilotRepository;
    private readonly IJobRepository _jobRepository;
    private readonly IApplicationRepository _applicationRepository;
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

    public async Task<ApiResponse<NaturalLanguageCandidateSearchResponseDto>> SearchCandidatesAsync(
        NaturalLanguageCandidateSearchRequest request,
        Guid? callerUserId,
        IReadOnlyCollection<string> callerRoles)
    {
        if (request.JobId == Guid.Empty)
        {
            return ApiResponse<NaturalLanguageCandidateSearchResponseDto>.BadRequest("JobId is required for v2 candidate search.");
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return ApiResponse<NaturalLanguageCandidateSearchResponseDto>.BadRequest("Search query is required.");
        }

        ApiResponse<CopilotCandidatePoolDto> poolResponse = await GetCandidatePoolAsync(request.JobId, callerUserId, callerRoles);
        if (!poolResponse.Success || poolResponse.Data is null)
        {
            return MirrorFailure<NaturalLanguageCandidateSearchResponseDto>(poolResponse.StatusCode, poolResponse.Message, poolResponse.ErrorCode);
        }

        CopilotCandidatePoolDto pool = poolResponse.Data;
        CopilotNormalizedRulesDto rules = BuildRules(request.Query, pool.Job.RequiredSkills, [], [], []);
        List<CopilotRankingResultDto> ranked = RankCandidates(pool.Candidates, rules);
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
            Ai = BuildDeterministicMetadata("Natural language extraction uses deterministic keyword/rule parsing in this v2 foundation slice.")
        };

        response = await TryGenerateStructuredResponseAsync(
            "candidate_search",
            "candidate_search",
            callerUserId,
            SearchInstructions(maxResults),
            new Dictionary<string, string>
            {
                ["query"] = request.Query.Trim(),
                ["jobTitle"] = pool.Job.Title
            },
            new
            {
                job = pool.Job,
                query = request.Query.Trim(),
                maxResults,
                deterministicFilters = rules,
                deterministicResults = results
            },
            response,
            ValidateSearchResponse,
            generated =>
            {
                generated.NormalizedIntent = string.IsNullOrWhiteSpace(generated.NormalizedIntent)
                    ? "candidate_search"
                    : generated.NormalizedIntent.Trim();
                generated.Query = string.IsNullOrWhiteSpace(generated.Query) ? request.Query.Trim() : generated.Query.Trim();
                generated.ExtractedFilters ??= rules;
                generated.Results = generated.Results
                    .Where(result => pool.Candidates.Any(candidate =>
                        candidate.CandidateUserId == result.CandidateUserId
                        && candidate.ApplicationId == result.ApplicationId))
                    .Take(maxResults)
                    .ToList();
            },
            generated => generated.Ai,
            (generated, metadata) => generated.Ai = metadata);

        await PersistArtifactAsync(
            callerUserId,
            request.JobId,
            null,
            "candidate_search",
            request.Query,
            response,
            response.Ai);

        return ApiResponse<NaturalLanguageCandidateSearchResponseDto>.Ok(response);
    }

    public async Task<ApiResponse<CandidateFitAnalysisResponseDto>> AnalyzeCandidateFitAsync(
        Guid jobId,
        CandidateFitAnalysisRequest request,
        Guid? callerUserId,
        IReadOnlyCollection<string> callerRoles)
    {
        ApiResponse<CopilotCandidatePoolDto> poolResponse = await GetCandidatePoolAsync(jobId, callerUserId, callerRoles);
        if (!poolResponse.Success || poolResponse.Data is null)
        {
            return MirrorFailure<CandidateFitAnalysisResponseDto>(poolResponse.StatusCode, poolResponse.Message, poolResponse.ErrorCode);
        }

        CopilotCandidatePoolDto pool = poolResponse.Data;
        CopilotNormalizedRulesDto rules = BuildRules(request.Prompt, pool.Job.RequiredSkills, [], [], []);
        HashSet<Guid> candidateFilter = request.CandidateUserIds.Where(id => id != Guid.Empty).ToHashSet();
        HashSet<Guid> applicationFilter = request.ApplicationIds.Where(id => id != Guid.Empty).ToHashSet();

        List<CopilotRankingResultDto> ranked = RankCandidates(pool.Candidates, rules)
            .Where(result =>
                candidateFilter.Count == 0 && applicationFilter.Count == 0
                || candidateFilter.Contains(result.CandidateUserId)
                || applicationFilter.Contains(result.ApplicationId))
            .ToList();

        CandidateFitAnalysisResponseDto response = new()
        {
            JobId = jobId,
            Analyses = ranked.Select(BuildFitAnalysis).ToList(),
            Ai = BuildDeterministicMetadata("Fit analysis uses deterministic scoring and ATS evidence; no provider call is required.")
        };

        response = await TryGenerateStructuredResponseAsync(
            "fit_analysis",
            "fit_analysis",
            callerUserId,
            FitAnalysisInstructions(),
            new Dictionary<string, string>
            {
                ["jobTitle"] = pool.Job.Title,
                ["prompt"] = request.Prompt ?? string.Empty
            },
            new
            {
                job = pool.Job,
                prompt = request.Prompt,
                candidateUserIds = request.CandidateUserIds,
                applicationIds = request.ApplicationIds,
                deterministicAnalyses = response.Analyses,
                candidates = ranked
            },
            response,
            ValidateFitAnalysisResponse,
            generated =>
            {
                generated.JobId = jobId;
                generated.Analyses = generated.Analyses
                    .Where(analysis => ranked.Any(candidate =>
                        candidate.CandidateUserId == analysis.CandidateUserId
                        && candidate.ApplicationId == analysis.ApplicationId))
                    .ToList();
            },
            generated => generated.Ai,
            (generated, metadata) => generated.Ai = metadata);

        foreach (CandidateFitAnalysisDto analysis in response.Analyses)
        {
            await _copilotRepository.AddFitAnalysisAsync(new CandidateFitAnalysis
            {
                AuditId = response.Ai.AuditId,
                JobId = jobId,
                CandidateUserId = analysis.CandidateUserId,
                ApplicationId = analysis.ApplicationId,
                FitLabel = analysis.FitLabel,
                ConfidenceScore = analysis.ConfidenceScore,
                TotalScore = analysis.TotalScore,
                StrengthsJson = JsonSerializer.Serialize(analysis.Strengths),
                GapsJson = JsonSerializer.Serialize(analysis.Gaps),
                EvidenceJson = JsonSerializer.Serialize(analysis.Evidence),
                Summary = analysis.Summary,
                ProviderName = response.Ai.ProviderName,
                ModelName = response.Ai.ModelName,
                FallbackUsed = response.Ai.FallbackUsed
            });
        }

        if (response.Analyses.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }

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

    public async Task<ApiResponse<ShortlistSuggestionResponseDto>> GenerateShortlistAsync(
        Guid jobId,
        ShortlistRequest request,
        Guid? callerUserId,
        IReadOnlyCollection<string> callerRoles)
    {
        ApiResponse<CopilotCandidatePoolDto> poolResponse = await GetCandidatePoolAsync(jobId, callerUserId, callerRoles);
        if (!poolResponse.Success || poolResponse.Data is null)
        {
            return MirrorFailure<ShortlistSuggestionResponseDto>(poolResponse.StatusCode, poolResponse.Message, poolResponse.ErrorCode);
        }

        CopilotCandidatePoolDto pool = poolResponse.Data;
        CopilotNormalizedRulesDto rules = BuildRules(request.Prompt, pool.Job.RequiredSkills, [], [], []);
        int maxCandidates = Math.Clamp(request.MaxCandidates <= 0 ? 5 : request.MaxCandidates, 1, 10);
        IReadOnlyList<ShortlistSuggestionDto> suggestions = RankCandidates(pool.Candidates, rules)
            .Where(result => !result.IsAutoRejected)
            .Take(maxCandidates)
            .Select(result => new ShortlistSuggestionDto
            {
                CandidateUserId = result.CandidateUserId,
                ApplicationId = result.ApplicationId,
                FullName = result.FullName,
                RankPosition = result.RankPosition,
                Score = result.TotalScore,
                Recommendation = result.Recommendation,
                Rationale = BuildRationale(result)
            })
            .ToList();

        ShortlistSuggestionResponseDto response = new()
        {
            JobId = jobId,
            Suggestions = suggestions,
            Ai = BuildDeterministicMetadata("Shortlist suggestions use deterministic ranking; shortlist publishing remains a separate HR action.")
        };

        response = await TryGenerateStructuredResponseAsync(
            "shortlist_suggestion",
            "shortlist_suggestion",
            callerUserId,
            ShortlistInstructions(maxCandidates),
            new Dictionary<string, string>
            {
                ["jobTitle"] = pool.Job.Title,
                ["prompt"] = request.Prompt ?? string.Empty
            },
            new
            {
                job = pool.Job,
                prompt = request.Prompt,
                maxCandidates,
                deterministicSuggestions = suggestions,
                candidates = pool.Candidates
            },
            response,
            ValidateShortlist,
            generated =>
            {
                generated.JobId = jobId;
                generated.Suggestions = generated.Suggestions
                    .Where(suggestion => pool.Candidates.Any(candidate =>
                        candidate.CandidateUserId == suggestion.CandidateUserId
                        && candidate.ApplicationId == suggestion.ApplicationId))
                    .Take(maxCandidates)
                    .ToList();
            },
            generated => generated.Ai,
            (generated, metadata) => generated.Ai = metadata);

        await PersistArtifactAsync(
            callerUserId,
            jobId,
            null,
            "shortlist_suggestion",
            request.Prompt,
            response,
            response.Ai);

        return ApiResponse<ShortlistSuggestionResponseDto>.Ok(response);
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
            return ApiResponse<HrEmailDraftResponseDto>.NotFound("Application not found");
        }

        if (!OwnershipScope.CanAccessApplication(application, callerUserId, callerRoles))
        {
            return ApiResponse<HrEmailDraftResponseDto>.Forbidden("Bạn không có quyền tạo email draft cho hồ sơ này.");
        }

        string candidateName = string.IsNullOrWhiteSpace(application.User?.FullName) ? "ứng viên" : application.User.FullName;
        string jobTitle = string.IsNullOrWhiteSpace(application.Job?.Title) ? "vị trí đang ứng tuyển" : application.Job.Title;
        string templateType = string.IsNullOrWhiteSpace(request.TemplateType) ? "screening_follow_up" : request.TemplateType.Trim();
        string tone = string.IsNullOrWhiteSpace(request.Tone) ? "professional" : request.Tone.Trim();

        (string subject, string body) = BuildEmailDraft(candidateName, jobTitle, templateType, tone, request.AdditionalInstruction);

        HrEmailDraftResponseDto response = new()
        {
            ApplicationId = applicationId,
            TemplateType = templateType,
            Subject = subject,
            Body = body,
            Evidence =
            [
                $"Candidate: {candidateName}",
                $"Job: {jobTitle}",
                $"Current status: {application.Status}"
            ],
            Ai = BuildDeterministicMetadata("Email draft uses approved deterministic templates; sending remains a separate endpoint.")
        };

        response = await TryGenerateStructuredResponseAsync(
            "email_draft",
            "email_draft",
            callerUserId,
            EmailInstructions(),
            new Dictionary<string, string>
            {
                ["candidateName"] = candidateName,
                ["jobTitle"] = jobTitle,
                ["templateType"] = templateType,
                ["tone"] = tone
            },
            new
            {
                applicationId,
                candidateName,
                jobTitle,
                currentStatus = application.Status.ToString(),
                templateType,
                tone,
                request.AdditionalInstruction,
                deterministicDraft = response
            },
            response,
            ValidateEmailDraft,
            generated =>
            {
                generated.ApplicationId = applicationId;
                generated.TemplateType = string.IsNullOrWhiteSpace(generated.TemplateType)
                    ? templateType
                    : generated.TemplateType.Trim();
            },
            generated => generated.Ai,
            (generated, metadata) => generated.Ai = metadata);

        await PersistArtifactAsync(
            callerUserId,
            application.JobId,
            applicationId,
            "email_draft",
            request.AdditionalInstruction,
            response,
            response.Ai);

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
            return ApiResponse<CopilotPromptTemplateDto>.BadRequest("Template name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return ApiResponse<CopilotPromptTemplateDto>.BadRequest("Template prompt is required.");
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
            return ApiResponse<CandidateFitAnalysisSnapshotDto>.NotFound("Application not found");
        }

        if (!OwnershipScope.CanAccessApplication(application, callerUserId, callerRoles))
        {
            return ApiResponse<CandidateFitAnalysisSnapshotDto>.Forbidden("Bạn không có quyền xem AI fit analysis của hồ sơ này.");
        }

        CandidateFitAnalysis? analysis = await _copilotRepository.GetLatestFitAnalysisAsync(applicationId);
        if (analysis is null)
        {
            return ApiResponse<CandidateFitAnalysisSnapshotDto>.NotFound("Fit analysis not found");
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
                "You are RecruitPro's AI Recruitment Copilot. Return only valid JSON matching the exact requested shape. Ground outputs in the provided ATS context and never mutate ATS state.",
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

    private static bool ValidateShortlist(ShortlistSuggestionResponseDto response)
    {
        return response.JobId != Guid.Empty
            && response.Suggestions.Count > 0
            && response.Suggestions.All(suggestion =>
                suggestion.CandidateUserId != Guid.Empty
                && suggestion.ApplicationId != Guid.Empty
                && !string.IsNullOrWhiteSpace(suggestion.FullName)
                && !string.IsNullOrWhiteSpace(suggestion.Recommendation));
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
        """;
    }

    private static string ShortlistInstructions(int maxCandidates)
    {
        return $$"""
        Generate shortlist suggestions using only the provided deterministic ranking and ATS evidence.
        Return this JSON shape:
        {
          "jobId": "uuid",
          "suggestions": [
            { "candidateUserId": "uuid", "applicationId": "uuid", "fullName": "name", "rankPosition": 1, "score": 0, "recommendation": "string", "rationale": [] }
          ]
        }
        Required: jobId, 1..{{maxCandidates}} suggestions, candidate/application ids, fullName, recommendation.
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
        return statusCode switch
        {
            400 => ApiResponse<T>.BadRequest(message, errorCode),
            403 => ApiResponse<T>.Forbidden(message, errorCode),
            404 => ApiResponse<T>.NotFound(message, errorCode),
            422 => ApiResponse<T>.UnprocessableEntity(message, errorCode: errorCode),
            _ => ApiResponse<T>.Error(message)
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
        string fitLabel = result.IsAutoRejected
            ? "NotRecommended"
            : result.TotalScore >= 80
                ? "StrongFit"
                : result.TotalScore >= 60
                    ? "PotentialFit"
                    : "RiskFit";

        return new CandidateFitAnalysisDto
        {
            CandidateUserId = result.CandidateUserId,
            ApplicationId = result.ApplicationId,
            FullName = result.FullName,
            FitLabel = fitLabel,
            ConfidenceScore = Math.Min(100, Math.Max(35, result.TotalScore)),
            TotalScore = result.TotalScore,
            Strengths = result.Strengths,
            Gaps = result.Weaknesses,
            Evidence = BuildRationale(result),
            Summary = BuildFitSummary(result, fitLabel)
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
                Category = "Technical",
                Question = $"Describe a recent project where you used {skill}. What trade-offs did you make?",
                Evidence = $"Job requires {skill}."
            });
        }

        questions.Add(new InterviewQuestionDto
        {
            Category = "Role Fit",
            Question = $"Which part of the {job.Title} role do you think would be the hardest for you in the first 60 days?",
            Evidence = $"Job context: {job.Title}."
        });

        questions.Add(new InterviewQuestionDto
        {
            Category = "Problem Solving",
            Question = "Tell us about a production issue you investigated from symptom to root cause. What did you measure first?",
            Evidence = "General engineering signal for structured debugging."
        });

        if (candidate is not null)
        {
            string candidateSkills = candidate.Skills.Count > 0
                ? string.Join(", ", candidate.Skills.Take(4))
                : "their listed experience";
            questions.Add(new InterviewQuestionDto
            {
                Category = "Candidate Evidence",
                Question = $"Your profile highlights {candidateSkills}. Which item best proves readiness for this role, and why?",
                Evidence = $"Candidate profile: {candidateSkills}."
            });
        }

        if (!string.Equals(focus, "job-fit", StringComparison.OrdinalIgnoreCase))
        {
            questions.Add(new InterviewQuestionDto
            {
                Category = "Custom Focus",
                Question = $"For the focus area \"{focus}\", what concrete evidence should we look for in this interview?",
                Evidence = "Recruiter-provided interview focus."
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
