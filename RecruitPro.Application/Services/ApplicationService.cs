using RecruitPro.Application.DTOs.Request.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Common;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Domain.Workflows;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace RecruitPro.Application.Services;

public class ApplicationService : IApplicationService
{
    private const string ScoreStatusPendingSemantic = "PendingSemantic";
    private const string ResumeParseStatusNotStarted = "NotStarted";
    private const string ResumeEmbeddingStatusNotStarted = "NotStarted";
    private readonly IApplicationRepository _applicationRepository;
    private readonly ICandidateProfileRepository _candidateProfileRepository;
    private readonly IUserRepository _userRepository;
    private readonly IJobRepository _jobRepository;
    private readonly IOfferRepository _offerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly IApplicationSemanticProcessingQueue _semanticProcessingQueue;
    private readonly INotificationEventService _notificationEventService;
    private readonly ILogger<ApplicationService> _logger;

    /// <summary>
    /// Initializes a new instance of the ApplicationService class.
    /// </summary>
    /// <param name="applicationRepository">The <paramref name="applicationRepository"/> value.</param>
    /// <param name="candidateProfileRepository">The <paramref name="candidateProfileRepository"/> value.</param>
    /// <param name="jobRepository">The <paramref name="jobRepository"/> value.</param>
    /// <param name="offerRepository">The <paramref name="offerRepository"/> value.</param>
    /// <param name="unitOfWork">The <paramref name="unitOfWork"/> value.</param>
    /// <param name="fileStorage">The <paramref name="fileStorage"/> value.</param>
    /// <param name="logger">The <paramref name="logger"/> value.</param>
    public ApplicationService(
        IApplicationRepository applicationRepository,
        ICandidateProfileRepository candidateProfileRepository,
        IUserRepository userRepository,
        IJobRepository jobRepository,
        IOfferRepository offerRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        IApplicationSemanticProcessingQueue semanticProcessingQueue,
        INotificationEventService notificationEventService,
        ILogger<ApplicationService> logger)
    {
        _applicationRepository = applicationRepository;
        _candidateProfileRepository = candidateProfileRepository;
        _userRepository = userRepository;
        _jobRepository = jobRepository;
        _offerRepository = offerRepository;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _semanticProcessingQueue = semanticProcessingQueue;
        _notificationEventService = notificationEventService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves apply screen.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ApplyJobScreenDto>> GetApplyScreenAsync(Guid userId, string jobId)
    {
        Job job = await GetJobAsync(jobId);
        CandidateProfile profile = await GetOrCreateProfileEntityAsync(userId);
        Domain.Entities.Application? existingApplication = await GetExistingApplicationAsync(userId, job.Id);
        bool hasActiveApplication = await _applicationRepository.HasActiveApplicationAsync(userId, job.Id);
        ApplyJobEligibilityDto eligibility = BuildApplyEligibility(job, profile, existingApplication, hasActiveApplication);

        CandidateResume? currentResume = GetCurrentResume(profile);
        ApplyJobResumeDto? resume = null;
        if (currentResume != null)
        {
            resume = new ApplyJobResumeDto
            {
                ResumeId = currentResume.Id.ToString(),
                FileName = currentResume.FileName,
                FileUrl = $"/api/resumes/{currentResume.Id}/preview",
                UploadedAt = currentResume.UploadDate
            };
        }

        return ApiResponse<ApplyJobScreenDto>.Ok(new ApplyJobScreenDto
        {
            Job = new ApplyJobJobSummaryDto
            {
                Id = job.Id.ToString(),
                Title = job.Title,
                DepartmentName = job.Department?.Name ?? "RecruitPro",
                Location = job.Location,
                WorkMode = job.WorkMode.ToString(),
                EmploymentType = job.EmploymentType.ToString(),
                SalaryMin = job.SalaryMin,
                SalaryMax = job.SalaryMax,
                SalaryLabel = CompensationLabelHelper.BuildSalaryLabel(job.SalaryMin, job.SalaryMax),
                VacancyCount = job.VacancyCount ?? 1,
                Status = job.Status.ToString(),
                Deadline = job.Deadline
            },
            CandidateProfile = new ApplyJobCandidateProfileDto
            {
                CandidateId = profile.Id.ToString(),
                FullName = profile.User.FullName,
                Email = profile.User.Email,
                Phone = profile.User.Phone,
                CurrentPosition = profile.CurrentPosition,
                ExperienceYears = profile.ExperienceYears,
                EditProfilePath = "/candidate/profile"
            },
            Resume = resume,
            Eligibility = eligibility
        });
    }

    /// <summary>
    /// Applies the requested data.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ApplyJobResponseDto>> ApplyAsync(Guid userId, string jobId, ApplyJobRequest request)
    {
        Job job = await GetJobAsync(jobId);
        CandidateProfile profile = await GetOrCreateProfileEntityAsync(userId);
        Domain.Entities.Application? existingApplication = await GetExistingApplicationAsync(userId, job.Id);
        // INV-003: the duplicate decision is EXISTS-active, evaluated by the DB independently of row
        // ordering — not "is the most recent/arbitrary row active".
        bool hasActiveApplication = await _applicationRepository.HasActiveApplicationAsync(userId, job.Id);
        ApplyJobEligibilityDto eligibility = BuildApplyEligibility(job, profile, existingApplication, hasActiveApplication);
        if (!eligibility.CanApply)
        {
            // Only an ACTIVE (non-closed) application means "already applied". A withdrawn/rejected
            // application is closed and must never produce the "already applied" message — that was
            // the bug where withdraw-then-reapply reported a duplicate. Map the active duplicate to
            // 409 Conflict and every other unmet business precondition to 422 with its real reason.
            if (hasActiveApplication)
            {
                return ApiResponse<ApplyJobResponseDto>.Conflict(
                    "Candidate already applied for this job.", errorCode: ErrorCodes.ApplicationAlreadyActive);
            }

            string message = eligibility.Blockers.FirstOrDefault() ?? "This job cannot be applied for right now.";
            return ApiResponse<ApplyJobResponseDto>.UnprocessableEntity(message, errorCode: eligibility.PrimaryErrorCode);
        }

        decimal ruleScore = CalculateRuleScore(profile, job);
        // BR-OWN-005: snapshot the recruiter and department-head owners at apply time so later changes
        // to the job's recruiter or the department's head do not silently re-route this application.
        //   AssignedRecruiterId      = Job.RecruiterId ?? Job.CreatedBy (audit fallback)
        //   AssignedDepartmentHeadId = Job.Department.HeadUserId ?? Job.ApprovedBy (audit fallback)
        Guid assignedRecruiterId = job.RecruiterId ?? job.CreatedBy;
        Guid? assignedDepartmentHeadId = job.Department?.HeadUserId ?? job.ApprovedBy;
        Domain.Entities.Application application = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobId = job.Id,
            AssignedRecruiterId = assignedRecruiterId,
            AssignedDepartmentHeadId = assignedDepartmentHeadId,
            Status = ApplicationStatus.Applied,
            AppliedAt = DbDateTime.Now,
            CoverLetter = string.IsNullOrWhiteSpace(request.CoverLetter)
                ? null
                : request.CoverLetter.Trim(),
            RuleScore = ruleScore,
            SemanticScore = null,
            FinalScore = ruleScore,
            ScoreStatus = ScoreStatusPendingSemantic,
            ScoredAt = DbDateTime.Now
        };
        try
        {
            await _unitOfWork.BeginTransactionAsync();
            await _applicationRepository.AddAsync(application);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
        }
        catch (DbUpdateException ex) when (IsActiveApplicationUniqueViolation(ex))
        {
            // INV-014: the DB partial unique index (ux_applications_active_user_job) is the last line of
            // defense against a concurrent double-apply that slipped past the service-level EXISTS check
            // (two requests both reading "no active application" before either commits). Map it to the
            // same stable 409 the service check produces, rather than letting it surface as a 500.
            await _unitOfWork.RollbackAsync();
            return ApiResponse<ApplyJobResponseDto>.Conflict(
                "Candidate already applied for this job.", errorCode: ErrorCodes.ApplicationAlreadyActive);
        }

        // The application is now durably committed: the apply succeeded. Everything below is a
        // best-effort side effect. A failure in notification dispatch or semantic-scoring enqueue
        // must NOT turn a successful apply into an HTTP 500 — that was the "normal apply randomly
        // returns 500" bug. Each side effect is isolated and logged, never propagated.
        try
        {
            // The job (and its JobSkills/Skills) was loaded AsNoTracking, so it must NOT be
            // linked onto the now-tracked application: the notification below calls
            // SaveChanges again, and EF would re-traverse that detached graph and try to
            // INSERT already-existing skills. Pass a throwaway, untracked entity that simply
            // carries the navigation values the notification needs to read.
            await _notificationEventService.PublishNewApplicationReceivedAsync(new Domain.Entities.Application
            {
                Id = application.Id,
                UserId = application.UserId,
                JobId = application.JobId,
                User = profile.User,
                Job = job
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Application {ApplicationId} was saved but the new-application notification failed to publish.",
                application.Id);
        }

        try
        {
            await _semanticProcessingQueue.EnqueueAsync(application.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Application {ApplicationId} was saved but enqueueing semantic scoring failed.",
                application.Id);
        }

        return ApiResponse<ApplyJobResponseDto>.Created(new ApplyJobResponseDto
        {
            ApplicationId = application.Id.ToString(),
            Status = application.Status.ToString(),
            RuleScore = application.RuleScore,
            SemanticScore = application.SemanticScore,
            FinalScore = application.FinalScore,
            ScoreStatus = application.ScoreStatus
        }, "Application submitted successfully");
    }

    /// <summary>
    /// Retrieves job applications.
    /// </summary>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <param name="page">The <paramref name="page"/> value.</param>
    /// <param name="pageSize">The <paramref name="pageSize"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>> GetJobApplicationsAsync(string jobId, int page = 1, int pageSize = 10)
    {
        Job job = await GetJobAsync(jobId);
        (IReadOnlyList<Domain.Entities.Application> applications, int total) = await _applicationRepository.GetByJobIdAsync(job.Id, page, pageSize);
        List<ApplicationListItemDto> items = applications.Select(MapApplicationToDto).ToList();

        return ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>.Ok(new PaginatedResponseDto<ApplicationListItemDto>
        {
            Items = items,
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = total
        });
    }

    /// <summary>
    /// Retrieves recent applications.
    /// </summary>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<IReadOnlyList<RecentJobApplicationDto>>> GetRecentApplicationsAsync(string jobId)
    {
        Job job = await GetJobAsync(jobId);
        IReadOnlyList<Domain.Entities.Application> applications = await _applicationRepository.GetRecentByJobIdAsync(job.Id, 5);

        List<RecentJobApplicationDto> items = applications.Select(application => new RecentJobApplicationDto
        {
            Id = application.Id.ToString(),
            CandidateId = application.UserId.ToString(),
            CandidateName = application.User.FullName,
            AvatarUrl = application.User.AvatarUrl,
            AppliedAt = application.AppliedAt,
            Status = application.Status.ToString(),
            Score = application.FinalScore ?? application.RuleScore
        }).ToList();

        return ApiResponse<IReadOnlyList<RecentJobApplicationDto>>.Ok(items);
    }

    /// <summary>
    /// Retrieves candidate applications.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="page">The <paramref name="page"/> value.</param>
    /// <param name="pageSize">The <paramref name="pageSize"/> value.</param>
    /// <param name="status">The <paramref name="status"/> value.</param>
    /// <param name="keyword">The <paramref name="keyword"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<CandidateApplicationsResponseDto>> GetCandidateApplicationsAsync(Guid userId, int page, int pageSize, string? status, string? keyword)
    {
        CandidateProfile profile = await GetOrCreateProfileEntityAsync(userId);
        IEnumerable<Domain.Entities.Application> query = await _applicationRepository.GetByUserIdAsync(profile.UserId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(application => application.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string loweredKeyword = keyword.Trim().ToLowerInvariant();
            query = query.Where(application => application.Job.Title.ToLower().Contains(loweredKeyword));
        }

        int total = query.Count();
        List<CandidateApplicationListItemDto> items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(application => new CandidateApplicationListItemDto
            {
                Id = application.Id.ToString(),
                JobId = application.JobId.ToString(),
                JobTitle = application.Job.Title,
                CompanyOrDepartment = application.Job.Department != null ? application.Job.Department.Name : "RecruitPro",
                AppliedDate = application.AppliedAt,
                // Canonical English enum value for logic; localized text moves to StatusLabel
                // (presentation only). Never return the Vietnamese label as Status.
                Status = application.Status.ToString(),
                StatusLabel = MapCandidateApplicationStatus(application),
                NextStep = BuildCandidateNextStep(application),
                AvailableActions = BuildCandidateAvailableActions(application)
            }).ToList();

        return ApiResponse<CandidateApplicationsResponseDto>.Ok(new CandidateApplicationsResponseDto
        {
            Items = items,
            Meta = PaginationMetaBuilder.Build(page, pageSize, total),
            Summary = new CandidateApplicationSummaryDto
            {
                Total = total,
                Active = query.Count(application => !ApplicationStatusWorkflow.IsClosed(application.Status)),
                Closed = query.Count(application => ApplicationStatusWorkflow.IsClosed(application.Status))
            }
        });
    }

    /// <summary>
    /// Withdraws application.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<string>> WithdrawApplicationAsync(Guid userId, string applicationId)
    {
        Domain.Entities.Application application = await GetTrackedApplicationForCandidateAsync(userId, applicationId);
        if (!ApplicationStatusWorkflow.CanCandidateWithdraw(application.Status))
        {
            return ApiResponse<string>.UnprocessableEntity(
                "This application can no longer be withdrawn.", errorCode: ErrorCodes.ApplicationNotWithdrawable);
        }

        // Withdrawal is candidate-initiated and non-punitive: it must be modelled as its own
        // closed state (Withdrawn), never as Rejected. Conflating it with Rejected mislabels the
        // candidate's history ("Không phù hợp") and is the reason re-apply used to be blocked.
        application.Status = ApplicationStatus.Withdrawn;
        // INV-008 / BR-008: leaving the pipeline invalidates any pending interview.
        CancelPendingInterviews(application);

        await _unitOfWork.BeginTransactionAsync();
        await _applicationRepository.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();
        return ApiResponse<string>.Ok("Đã rút đơn ứng tuyển.", "Đã rút đơn ứng tuyển.");
    }

    /// <summary>
    /// Accepts offer.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<string>> AcceptOfferAsync(Guid userId, string applicationId)
    {
        Domain.Entities.Application application = await GetTrackedApplicationForCandidateAsync(userId, applicationId);

        // INV-009: Hired must come only from the candidate accepting a Sent offer while the application
        // is in Offer. No reviewer/Hired bypass — a business-state failure is 422, not 400.
        if (!ApplicationStatusWorkflow.CanCandidateRespondToOffer(application.Status))
        {
            return ApiResponse<string>.UnprocessableEntity(
                "This application is not waiting for an offer response.", errorCode: ErrorCodes.OfferNotActionable);
        }

        ApplicationOffer? offer = await _offerRepository.GetTrackedByApplicationIdAsync(application.Id);
        if (offer?.Status != OfferStatus.Sent)
        {
            return ApiResponse<string>.UnprocessableEntity(
                "An offer has not been sent for this application yet.", errorCode: ErrorCodes.OfferNotActionable);
        }

        application.Status = ApplicationStatus.Hired;
        offer.Status = OfferStatus.Accepted;
        offer.UpdatedAt = DbDateTime.Now;

        await _unitOfWork.BeginTransactionAsync();
        await _offerRepository.UpdateAsync(offer);
        await _applicationRepository.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();

        return ApiResponse<string>.Ok("Đã nhận offer.", "Đã nhận offer.");
    }

    /// <summary>
    /// Declines offer.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<string>> DeclineOfferAsync(Guid userId, string applicationId)
    {
        Domain.Entities.Application application = await GetTrackedApplicationForCandidateAsync(userId, applicationId);
        if (!ApplicationStatusWorkflow.CanCandidateRespondToOffer(application.Status))
        {
            return ApiResponse<string>.UnprocessableEntity(
                "This application is not waiting for an offer response.", errorCode: ErrorCodes.OfferNotActionable);
        }

        ApplicationOffer? offer = await _offerRepository.GetTrackedByApplicationIdAsync(application.Id);
        if (offer?.Status != OfferStatus.Sent)
        {
            return ApiResponse<string>.UnprocessableEntity(
                "An offer has not been sent for this application yet.", errorCode: ErrorCodes.OfferNotActionable);
        }

        application.Status = ApplicationStatus.OfferDeclined;
        offer.Status = OfferStatus.Declined;
        offer.UpdatedAt = DbDateTime.Now;

        await _unitOfWork.BeginTransactionAsync();
        await _offerRepository.UpdateAsync(offer);
        await _applicationRepository.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();

        return ApiResponse<string>.Ok("Đã từ chối offer.", "Đã từ chối offer.");
    }

    /// <summary>
    /// Retrieves hr applications.
    /// </summary>
    /// <param name="page">The <paramref name="page"/> value.</param>
    /// <param name="pageSize">The <paramref name="pageSize"/> value.</param>
    /// <param name="keyword">The <paramref name="keyword"/> value.</param>
    /// <param name="department">The <paramref name="department"/> value.</param>
    /// <param name="status">The <paramref name="status"/> value.</param>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>> GetHrApplicationsAsync(int page, int pageSize, string? keyword, string? department, string? status, string? jobId)
    {
        ApplicationStatus? parsedStatus = ParseApplicationStatus(status);
        Guid? parsedJobId = Guid.TryParse(jobId, out Guid jobGuid) ? jobGuid : null;
        (IReadOnlyList<Domain.Entities.Application> applications, int total) = await _applicationRepository.GetPagedAsync(page, pageSize, keyword, department, parsedStatus, parsedJobId);

        return ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>.Ok(new PaginatedResponseDto<ApplicationListItemDto>
        {
            Items = applications.Select(MapApplicationToDto).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = total
        });
    }

    /// <summary>
    /// Retrieves manager review queue.
    /// </summary>
    /// <param name="page">The <paramref name="page"/> value.</param>
    /// <param name="pageSize">The <paramref name="pageSize"/> value.</param>
    /// <param name="keyword">The <paramref name="keyword"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ManagerReviewQueueResponseDto>> GetManagerReviewQueueAsync(int page, int pageSize, string? keyword)
    {
        IReadOnlyList<Domain.Entities.Application> queueApplications = await _applicationRepository.GetManagerReviewQueueAsync(keyword);
        List<ManagerReviewQueueItemDto> queueItems = queueApplications
            .Select(MapManagerReviewQueueItem)
            .ToList();

        int safePage = Math.Max(page, 1);
        int safePageSize = Math.Max(pageSize, 1);

        List<ManagerReviewQueueItemDto> pagedItems = queueItems
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToList();

        int recommendedCount = queueItems.Count(item =>
            item.Recommendation.Equals("Strong Hire", StringComparison.OrdinalIgnoreCase) ||
            item.Recommendation.Equals("Hire", StringComparison.OrdinalIgnoreCase));

        return ApiResponse<ManagerReviewQueueResponseDto>.Ok(new ManagerReviewQueueResponseDto
        {
            Items = pagedItems,
            Meta = PaginationMetaBuilder.Build(safePage, safePageSize, queueItems.Count),
            Summary = new ManagerReviewQueueSummaryDto
            {
                PendingFinalApprovals = queueItems.Count,
                RecommendedCount = recommendedCount,
                FlaggedCount = Math.Max(queueItems.Count - recommendedCount, 0),
                AverageScore = queueItems.Count == 0 ? 0 : Math.Round(queueItems.Average(item => item.Score), 1)
            }
        });
    }

    /// <summary>
    /// Retrieves application review detail.
    /// </summary>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ApplicationReviewDetailDto>> GetApplicationReviewDetailAsync(string applicationId)
    {
        if (!Guid.TryParse(applicationId, out Guid applicationGuid))
        {
            return ApiResponse<ApplicationReviewDetailDto>.NotFound("Không tìm thấy hồ sơ ứng tuyển.");
        }

        Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(applicationGuid);
        if (application == null)
        {
            return ApiResponse<ApplicationReviewDetailDto>.NotFound("Không tìm thấy hồ sơ ứng tuyển.");
        }

        return ApiResponse<ApplicationReviewDetailDto>.Ok(MapApplicationToReviewDetailDto(application));
    }

    /// <summary>
    /// Updates application decision.
    /// </summary>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <param name="reviewerId">The <paramref name="reviewerId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ApplicationReviewDetailDto>> UpdateApplicationDecisionAsync(
        string applicationId,
        Guid? reviewerId,
        UpdateApplicationDecisionRequest request)
    {
        if (!Guid.TryParse(applicationId, out Guid applicationGuid))
        {
            return ApiResponse<ApplicationReviewDetailDto>.NotFound("Không tìm thấy hồ sơ ứng tuyển.");
        }

        Domain.Entities.Application? application = await _applicationRepository.GetTrackedByIdAsync(applicationGuid);
        if (application == null)
        {
            return ApiResponse<ApplicationReviewDetailDto>.NotFound("Không tìm thấy hồ sơ ứng tuyển.");
        }

        ApplicationStatus? targetStatus = ParseApplicationStatus(request.TargetStatus);
        if (!targetStatus.HasValue)
        {
            // Malformed input (unparseable status string) is a 400; an invalid but well-formed
            // transition is a business-state failure (422) — see below.
            return ApiResponse<ApplicationReviewDetailDto>.BadRequest("Target application status is invalid.");
        }

        // INV-009: Hired and OfferDeclined are candidate-owned outcomes of an offer response. A
        // reviewer (HR/Manager) must not drive the application out of Offer via this decision endpoint;
        // those transitions only happen through accept-offer / decline-offer.
        if (application.Status == ApplicationStatus.Offer)
        {
            return ApiResponse<ApplicationReviewDetailDto>.UnprocessableEntity(
                "An offer outcome must come from the candidate accepting or declining the offer.",
                errorCode: ErrorCodes.InvalidApplicationTransition);
        }

        if (!ApplicationStatusWorkflow.CanTransition(application.Status, targetStatus.Value))
        {
            return ApiResponse<ApplicationReviewDetailDto>.UnprocessableEntity(
                $"Invalid transition from {application.Status} to {targetStatus.Value}.",
                errorCode: ErrorCodes.InvalidApplicationTransition);
        }

        // BR-OWN-007 — ManagerReview = the DepartmentHeadReview business stage. Advancing a ManagerReview
        // application (to Interview or Rejected) is reserved for the application's assigned DepartmentHead
        // or a SystemAdmin. The earlier HR-owned stages (Applied→Screening, Screening→ManagerReview) and
        // the Interview→Offer/Rejected stage keep their existing HR/Manager behavior.
        if (application.Status == ApplicationStatus.ManagerReview
            && targetStatus.Value is ApplicationStatus.Interview or ApplicationStatus.Rejected)
        {
            ApiResponse<ApplicationReviewDetailDto>? guardFailure = await GuardManagerReviewDecisionAsync(application, reviewerId);
            if (guardFailure != null)
            {
                return guardFailure;
            }
        }

        ApplicationStatus previousStatus = application.Status;
        application.Status = targetStatus.Value;

        if (reviewerId.HasValue)
        {
            application.ReviewedBy = reviewerId.Value;
        }

        // INV-008 / BR-008: a reviewer rejection takes the application out of the pipeline; any pending
        // interview must be cancelled.
        if (targetStatus.Value == ApplicationStatus.Rejected)
        {
            CancelPendingInterviews(application);
        }

        await _unitOfWork.BeginTransactionAsync();
        await _applicationRepository.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();

        // INV-010: the status change is committed; notification is a best-effort side effect and must
        // never turn a committed transition into a 500.
        try
        {
            await _notificationEventService.PublishApplicationStatusChangedAsync(application, previousStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Application {ApplicationId} transitioned to {Status} but the status-changed notification failed to publish.",
                application.Id,
                application.Status);
        }

        Domain.Entities.Application? refreshedApplication = await _applicationRepository.GetByIdAsync(applicationGuid);
        if (refreshedApplication == null)
        {
            return ApiResponse<ApplicationReviewDetailDto>.NotFound("Không tìm thấy hồ sơ ứng tuyển.");
        }

        string message = $"Application moved to {targetStatus.Value}.";

        return ApiResponse<ApplicationReviewDetailDto>.Ok(MapApplicationToReviewDetailDto(refreshedApplication), message);
    }

    /// <summary>
    /// Retrieves application cv.
    /// </summary>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ResumeFileResponseDto>> GetApplicationCvAsync(string applicationId)
    {
        if (!Guid.TryParse(applicationId, out Guid applicationGuid))
        {
            return ApiResponse<ResumeFileResponseDto>.NotFound("Không tìm thấy hồ sơ ứng tuyển.");
        }

        Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(applicationGuid);
        if (application == null)
        {
            return ApiResponse<ResumeFileResponseDto>.NotFound("Không tìm thấy hồ sơ ứng tuyển.");
        }

        CandidateResume? resume = application.User.CandidateProfile == null
            ? null
            : GetCurrentResume(application.User.CandidateProfile);
        if (resume == null)
        {
            return ApiResponse<ResumeFileResponseDto>.NotFound("Không tìm thấy CV.");
        }

        _logger.LogInformation(
            "Resolved internal resume preview URL for application {ApplicationId} and candidate {CandidateId}.",
            application.Id,
            application.UserId);

        return ApiResponse<ResumeFileResponseDto>.Ok(new ResumeFileResponseDto
        {
            ResumeId = resume.Id.ToString(),
            FileName = resume.FileName,
            FileUrl = $"/api/resumes/{resume.Id}/preview"
        });
    }

    /// <summary>
    /// Sends application email.
    /// </summary>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<string>> SendApplicationEmailAsync(string applicationId, SendApplicationEmailRequest request)
    {
        if (!Guid.TryParse(applicationId, out Guid applicationGuid))
        {
            return ApiResponse<string>.NotFound("Không tìm thấy hồ sơ ứng tuyển.");
        }

        Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(applicationGuid);
        if (application == null)
        {
            return ApiResponse<string>.NotFound("Không tìm thấy hồ sơ ứng tuyển.");
        }

        string recipient = application.User.Email;
        string normalizedTemplate = string.IsNullOrWhiteSpace(request.TemplateType) ? "General" : request.TemplateType.Trim();
        string effectiveSubject = string.IsNullOrWhiteSpace(request.Subject) ? $"{normalizedTemplate} - {application.Job.Title}" : request.Subject.Trim();
        string effectiveBody = string.IsNullOrWhiteSpace(request.Body)
            ? $"Prepared {normalizedTemplate} email for {recipient} regarding {application.Job.Title}."
            : request.Body.Trim();

        return ApiResponse<string>.Ok($"{effectiveSubject}: {effectiveBody}", $"Đã soạn email cho {recipient}");
    }

    /// <summary>
    /// Retrieves existing application.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private async Task<Domain.Entities.Application?> GetExistingApplicationAsync(Guid userId, Guid jobId)
    {
        IReadOnlyList<Domain.Entities.Application> existingApplications = await _applicationRepository.GetByUserIdAsync(userId);
        List<Domain.Entities.Application> forJob = existingApplications
            .Where(application => application.JobId == jobId)
            .ToList();

        // INV-003: never depend on an arbitrary/"latest" row. If an ACTIVE application exists it is the
        // candidate's current relationship with the job; otherwise fall back to the most recent closed
        // row (GetByUserIdAsync is ordered by AppliedAt desc) for history/Hired-blocker context.
        return forJob.FirstOrDefault(application => !ApplicationStatusWorkflow.IsClosed(application.Status))
            ?? forJob.FirstOrDefault();
    }

    /// <summary>
    /// Builds apply eligibility.
    /// </summary>
    /// <param name="job">The <paramref name="job"/> value.</param>
    /// <param name="profile">The <paramref name="profile"/> value.</param>
    /// <param name="existingApplication">The <paramref name="existingApplication"/> value.</param>
    /// <returns>The operation result.</returns>
    private static ApplyJobEligibilityDto BuildApplyEligibility(
        Job job,
        CandidateProfile profile,
        Domain.Entities.Application? existingApplication,
        bool hasActiveApplication)
    {
        // Each blocker carries a stable machine error code (the first one becomes PrimaryErrorCode for
        // the 4xx response). Order matters: the duplicate/hired checks come last so a real precondition
        // failure is surfaced instead of being masked by history.
        List<(string Code, string Message)> blockers = [];

        if (job.Status != JobStatus.Approved)
        {
            blockers.Add((ErrorCodes.JobNotAcceptingApplications, "This job posting is not accepting new applications."));
        }

        if (job.Deadline.HasValue && job.Deadline.Value < DbDateTime.Now)
        {
            blockers.Add((ErrorCodes.JobDeadlinePassed, "The application deadline for this job has passed."));
        }

        if (string.IsNullOrWhiteSpace(profile.User.FullName) || string.IsNullOrWhiteSpace(profile.User.Email))
        {
            blockers.Add((ErrorCodes.CandidateProfileIncomplete, "Your profile is missing required contact information."));
        }

        if (GetCurrentResume(profile) == null)
        {
            blockers.Add((ErrorCodes.ResumeRequired, "Please upload your latest resume before applying."));
        }

        if (hasActiveApplication)
        {
            blockers.Add((ErrorCodes.ApplicationAlreadyActive, "You have already applied for this job."));
        }
        // INV-015: Hired is closed-for-workflow but terminal for this jobId. A prior Hired (and no
        // active application) blocks a fresh apply for the SAME job — this is a business blocker (422),
        // not an active duplicate (409). Re-apply-eligible closed states (Rejected/Withdrawn/
        // OfferDeclined) deliberately do NOT block.
        else if (existingApplication is { Status: ApplicationStatus.Hired })
        {
            blockers.Add((ErrorCodes.ApplicationAlreadyHired, "You have already been hired for this job."));
        }

        return new ApplyJobEligibilityDto
        {
            CanApply = blockers.Count == 0,
            // "Already applied" must reflect a live application only. A withdrawn/rejected/closed
            // record is history, not an active application, and must not block or mislabel re-apply.
            AlreadyApplied = hasActiveApplication,
            ExistingApplicationId = existingApplication?.Id.ToString(),
            ExistingApplicationStatus = existingApplication?.Status.ToString(),
            Blockers = blockers.Select(blocker => blocker.Message).ToList(),
            PrimaryErrorCode = blockers.Count == 0 ? null : blockers[0].Code,
            GuidanceMessage = blockers.Count == 0
                ? "Your application will be submitted to the recruitment team for review."
                : hasActiveApplication
                    ? "Track the latest status of this application from My Applications."
                    : "Complete the missing requirements before submitting your application."
        };
    }

    /// <summary>
    /// Cancels any pending (Scheduled) interview when the application leaves the active pipeline
    /// (Withdrawn/Rejected). INV-008 / BR-008: an interview is only actionable while the application
    /// is in the Interview stage; once it leaves, pending interviews must not remain actionable.
    /// Operates on the tracked application's loaded Interviews collection so it is saved in the same
    /// transaction as the status change.
    /// </summary>
    private static void CancelPendingInterviews(Domain.Entities.Application application)
    {
        foreach (Interview interview in application.Interviews)
        {
            if (interview.Status == InterviewStatus.Scheduled)
            {
                interview.Status = InterviewStatus.Canceled;
            }
        }
    }

    /// <summary>
    /// Determines whether a <see cref="DbUpdateException"/> was caused by the active-application
    /// partial unique index. Checked by the constraint name plus the PostgreSQL unique-violation
    /// SQLSTATE (23505) carried on the inner exception, without taking a hard dependency on Npgsql.
    /// </summary>
    private static bool IsActiveApplicationUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            string message = current.Message;
            if (message.Contains("ux_applications_active_user_job", StringComparison.OrdinalIgnoreCase)
                || message.Contains("23505", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Authorizes a ManagerReview (DepartmentHeadReview) decision against the application's assigned
    /// department head (resolved via the shared ownership logic) or a SystemAdmin. Returns a non-null
    /// 403 failure to short-circuit; null when allowed. Temporary migration fallback (BR-OWN-007): when
    /// no head was snapshotted, a Manager-role user may still act.
    /// </summary>
    private async Task<ApiResponse<ApplicationReviewDetailDto>?> GuardManagerReviewDecisionAsync(
        Domain.Entities.Application application,
        Guid? reviewerId)
    {
        Guid? assignedHeadId = ApplicationOwnershipResolver.Resolve(application).DepartmentHeadUserId;

        if (reviewerId.HasValue && assignedHeadId.HasValue && reviewerId.Value == assignedHeadId.Value)
        {
            return null;
        }

        IReadOnlyCollection<string> reviewerRoles = await GetUserRoleNamesAsync(reviewerId);
        if (reviewerRoles.Contains(RoleNames.SystemAdmin))
        {
            return null;
        }

        if (assignedHeadId == null && reviewerRoles.Contains(RoleNames.Manager))
        {
            return null;
        }

        return ApiResponse<ApplicationReviewDetailDto>.Forbidden(
            "Only the assigned department head or a system administrator can advance this application from manager review.",
            errorCode: ErrorCodes.Forbidden);
    }

    private async Task<IReadOnlyCollection<string>> GetUserRoleNamesAsync(Guid? userId)
    {
        if (!userId.HasValue)
        {
            return [];
        }

        User? user = await _userRepository.GetByIdAsync(userId.Value);
        return user == null
            ? []
            : user.UserRoles.Select(userRole => userRole.Role.Name).ToArray();
    }

    /// <summary>
    /// Retrieves job.
    /// </summary>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="NotFoundException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    private async Task<Job> GetJobAsync(string jobId)
    {
        if (!Guid.TryParse(jobId, out Guid jobGuid))
        {
            throw new NotFoundException($"Job with ID {jobId} not found.");
        }

        Job? job = await _jobRepository.GetByIdAsync(jobGuid);
        if (job == null)
        {
            throw new NotFoundException($"Job with ID {jobId} not found.");
        }

        return job;
    }

    /// <summary>
    /// Retrieves profile entity.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="NotFoundException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    private async Task<CandidateProfile> GetProfileEntityAsync(Guid userId)
    {
        CandidateProfile? profile = await _candidateProfileRepository.GetByUserIdAsync(userId);
        if (profile == null)
        {
            throw new NotFoundException("Candidate profile not found.");
        }

        return profile;
    }

    private async Task<CandidateProfile> GetOrCreateProfileEntityAsync(Guid userId)
    {
        CandidateProfile? profile = await _candidateProfileRepository.GetByUserIdAsync(userId);
        if (profile != null)
        {
            return profile;
        }

        User? user = await _userRepository.GetTrackedByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy người dùng.");
        }

        CandidateProfile createdProfile = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            ResumeParseStatus = ResumeParseStatusNotStarted,
            CandidateEmbeddingStatus = ResumeEmbeddingStatusNotStarted
        };

        user.CandidateProfile = createdProfile;
        await _candidateProfileRepository.SaveAsync(createdProfile);
        await _unitOfWork.SaveChangesAsync();

        return createdProfile;
    }

    /// <summary>
    /// Retrieves tracked application for candidate.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="NotFoundException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    private async Task<Domain.Entities.Application> GetTrackedApplicationForCandidateAsync(Guid userId, string applicationId)
    {
        CandidateProfile profile = await GetOrCreateProfileEntityAsync(userId);
        if (!Guid.TryParse(applicationId, out Guid applicationGuid))
        {
            throw new NotFoundException("Không tìm thấy hồ sơ ứng tuyển.");
        }

        Domain.Entities.Application? application = await _applicationRepository.GetTrackedByIdAsync(applicationGuid);
        if (application == null || application.UserId != profile.UserId)
        {
            throw new NotFoundException("Không tìm thấy hồ sơ ứng tuyển.");
        }

        return application;
    }

    /// <summary>
    /// Maps application to dto.
    /// </summary>
    /// <param name="application">The <paramref name="application"/> value.</param>
    /// <returns>The operation result.</returns>
    private static ApplicationListItemDto MapApplicationToDto(Domain.Entities.Application application)
    {
        (double fallbackScore, _) = BuildReviewScore(application);
        decimal? effectiveFinalScore = application.FinalScore ?? application.RuleScore;

        return new ApplicationListItemDto
        {
            Id = application.Id.ToString(),
            Candidate = new ApplicationCandidateSummaryDto
            {
                Id = application.User.CandidateProfile?.Id.ToString() ?? application.UserId.ToString(),
                FullName = application.User.FullName,
                Email = application.User.Email,
                AvatarUrl = application.User.AvatarUrl,
                CurrentPosition = application.User.CandidateProfile?.CurrentPosition
            },
            Job = new ApplicationJobSummaryDto
            {
                Id = application.Job.Id.ToString(),
                Title = application.Job.Title,
                Department = new DepartmentDto
                {
                    Id = application.Job.Department?.Id.ToString() ?? string.Empty,
                    Name = application.Job.Department?.Name ?? string.Empty,
                    Description = application.Job.Department?.Description
                }
            },
            Status = application.Status.ToString(),
            AppliedAt = application.AppliedAt ?? DbDateTime.Now,
            ReviewedBy = application.ReviewedByNavigation == null ? null : new UserDto
            {
                Id = application.ReviewedByNavigation.Id,
                Username = application.ReviewedByNavigation.Username,
                FullName = application.ReviewedByNavigation.FullName,
                Email = application.ReviewedByNavigation.Email,
                AvatarUrl = application.ReviewedByNavigation.AvatarUrl,
                Phone = application.ReviewedByNavigation.Phone,
                Roles = application.ReviewedByNavigation.UserRoles.Select(userRole => userRole.Role.Name).ToList()
            },
            AssignedRecruiterId = application.AssignedRecruiterId?.ToString(),
            AssignedRecruiterName = application.AssignedRecruiter?.FullName,
            AssignedRecruiterEmail = application.AssignedRecruiter?.Email,
            AssignedDepartmentHeadId = application.AssignedDepartmentHeadId?.ToString(),
            AssignedDepartmentHeadName = application.AssignedDepartmentHead?.FullName,
            AssignedDepartmentHeadEmail = application.AssignedDepartmentHead?.Email,
            Score = (double?)effectiveFinalScore ?? fallbackScore,
            RuleScore = application.RuleScore,
            SemanticScore = application.SemanticScore,
            FinalScore = application.FinalScore,
            ScoreStatus = application.ScoreStatus,
            NextStep = BuildReviewNextStep(application)
        };
    }

    private static decimal CalculateRuleScore(CandidateProfile profile, Job job)
    {
        Dictionary<string, decimal?> candidateSkillYears = new(StringComparer.OrdinalIgnoreCase);
        foreach (CandidateSkill candidateSkill in profile.CandidateSkills)
        {
            string? skillName = candidateSkill.Skill?.Name;
            if (!string.IsNullOrWhiteSpace(skillName))
            {
                candidateSkillYears[skillName] = candidateSkill.YearsOfExperience;
            }
        }

        List<JobSkill> requiredSkills = job.JobSkills.Where(item => item.IsRequired).ToList();
        List<JobSkill> niceToHaveSkills = job.JobSkills.Where(item => !item.IsRequired).ToList();

        decimal requiredSkillScore = requiredSkills.Count == 0
            ? 40
            : (decimal)requiredSkills.Count(skill => candidateSkillYears.ContainsKey(skill.Skill.Name)) / requiredSkills.Count * 40m;

        decimal experienceScore = 0;
        if (job.MinExperienceYears.GetValueOrDefault() <= 0)
        {
            experienceScore = 20;
        }
        else
        {
            decimal candidateExperience = profile.ExperienceYears ?? 0;
            experienceScore = Math.Min(candidateExperience / job.MinExperienceYears.Value, 1m) * 20m;
        }

        decimal niceToHaveScore = niceToHaveSkills.Count == 0
            ? 15
            : (decimal)niceToHaveSkills.Count(skill => candidateSkillYears.ContainsKey(skill.Skill.Name)) / niceToHaveSkills.Count * 15m;

        decimal keywordScore = CalculateKeywordScore(profile, job) * 15m;

        decimal total = requiredSkillScore + experienceScore + niceToHaveScore + keywordScore + 10m;
        return Math.Round(Math.Min(Math.Max(total, 0), 100), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateKeywordScore(CandidateProfile profile, Job job)
    {
        HashSet<string> candidateTerms = BuildCandidateKeywordSet(profile);
        HashSet<string> jobTerms = BuildJobKeywordSet(job);
        if (jobTerms.Count == 0)
        {
            return 1m;
        }

        int matchedTerms = jobTerms.Count(candidateTerms.Contains);
        return matchedTerms / (decimal)jobTerms.Count;
    }

    private static HashSet<string> BuildCandidateKeywordSet(CandidateProfile profile)
    {
        List<string> tokens =
        [
            profile.CurrentPosition ?? string.Empty,
            profile.Bio ?? string.Empty,
            profile.Education ?? string.Empty,
            profile.Address ?? string.Empty,
            string.Join(' ', profile.CandidateSkills.Select(skill => skill.Skill.Name)),
            string.Join(' ', profile.Projects.Select(project => $"{project.Name} {project.Role} {project.Description}"))
        ];

        return TokenizeKeywords(string.Join(' ', tokens));
    }

    private static HashSet<string> BuildJobKeywordSet(Job job)
    {
        List<string> tokens =
        [
            job.Title,
            job.ShortPitch ?? string.Empty,
            job.Description,
            job.Requirements ?? string.Empty,
            job.Location,
            string.Join(' ', job.JobSkills.Select(skill => skill.Skill.Name))
        ];

        return TokenizeKeywords(string.Join(' ', tokens));
    }

    private static HashSet<string> TokenizeKeywords(string text)
    {
        string[] stopWords = ["and", "the", "with", "for", "from", "that", "this", "have", "has", "you", "your"];
        return Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9.+#]+")
            .Select(token => token.Trim())
            .Where(token => token.Length >= 2 && !stopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maps application to review detail dto.
    /// </summary>
    /// <param name="application">The <paramref name="application"/> value.</param>
    /// <returns>The operation result.</returns>
    private static ApplicationReviewDetailDto MapApplicationToReviewDetailDto(Domain.Entities.Application application)
    {
        List<string> candidateSkills = application.User.CandidateProfile?.CandidateSkills
            .Select(skill => skill.Skill.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList() ?? [];

        List<string> requiredSkills = application.Job.JobSkills
            .Where(jobSkill => jobSkill.IsRequired)
            .Select(jobSkill => jobSkill.Skill.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();

        int matchedSkillCount = requiredSkills.Count(requiredSkill =>
            candidateSkills.Any(candidateSkill => candidateSkill.Equals(requiredSkill, StringComparison.OrdinalIgnoreCase)));

        int skillsMatchPercent = requiredSkills.Count == 0
            ? 100
            : (int)Math.Round((double)matchedSkillCount * 100 / requiredSkills.Count, MidpointRounding.AwayFromZero);

        List<Interview> orderedInterviews = application.Interviews
            .OrderBy(interview => interview.InterviewDate)
            .ToList();

        return new ApplicationReviewDetailDto
        {
            ApplicationId = application.Id.ToString(),
            ReferenceCode = BuildReferenceCode(application.Id),
            StageLabel = BuildStageLabel(application),
            Status = application.Status.ToString(),
            OfferStatus = application.Offer?.Status.ToString(),
            AppliedAt = application.AppliedAt,
            NextStep = application.Status switch
            {
                ApplicationStatus.Applied => "Đã nhận hồ sơ",
                ApplicationStatus.Screening => "HR đang sàng lọc hồ sơ.",
                ApplicationStatus.ManagerReview => "Chờ quản lý tuyển dụng đánh giá hồ sơ.",
                ApplicationStatus.Interview => "Ứng viên đang ở vòng phỏng vấn.",
                ApplicationStatus.Offer => "Đang xử lý offer cho ứng viên.",
                ApplicationStatus.Hired => "Ứng viên đã chấp nhận offer.",
                ApplicationStatus.OfferDeclined => "Ứng viên đã từ chối offer.",
                ApplicationStatus.Rejected => "Hồ sơ đã bị từ chối.",
                ApplicationStatus.Withdrawn => "Ứng viên đã rút đơn ứng tuyển.",
                _ => orderedInterviews.Any() ? "Theo dõi lịch phỏng vấn." : "Tiếp tục xử lý hồ sơ."
            },
            Candidate = new ApplicationReviewCandidateDto
            {
                Id = application.User.CandidateProfile?.Id.ToString() ?? application.UserId.ToString(),
                FullName = application.User.FullName,
                Email = application.User.Email,
                Phone = application.User.Phone,
                AvatarUrl = application.User.AvatarUrl,
                CurrentPosition = application.User.CandidateProfile?.CurrentPosition,
                ExperienceYears = application.User.CandidateProfile?.ExperienceYears,
                Education = application.User.CandidateProfile?.Education,
                Address = application.User.CandidateProfile?.Address,
                Bio = application.User.CandidateProfile?.Bio,
                LinkedinUrl = application.User.CandidateProfile?.LinkedinUrl,
                GithubUrl = application.User.CandidateProfile?.GithubUrl,
                Skills = candidateSkills
            },
            Job = new ApplicationReviewJobDto
            {
                Id = application.JobId.ToString(),
                Title = application.Job.Title,
                DepartmentName = application.Job.Department?.Name ?? "RecruitPro",
                RequiredSkills = requiredSkills
            },
            Insights = new ApplicationReviewInsightDto
            {
                SkillsMatchPercent = skillsMatchPercent,
                MatchedSkillCount = matchedSkillCount,
                RequiredSkillCount = requiredSkills.Count,
                SubmittedInterviewNotes = orderedInterviews.Count(interview => !string.IsNullOrWhiteSpace(interview.Notes)),
                TotalInterviews = orderedInterviews.Count
            },
            Interviews = orderedInterviews.Select((interview, index) => new ApplicationReviewInterviewDto
            {
                Id = interview.Id.ToString(),
                Label = $"Interview Round {index + 1}",
                InterviewDate = interview.InterviewDate,
                Status = (interview.Status ?? InterviewStatus.Scheduled).ToString(),
                Notes = interview.Notes
            }).ToList(),
            ReviewedBy = application.ReviewedByNavigation == null ? null : new UserDto
            {
                Id = application.ReviewedByNavigation.Id,
                Username = application.ReviewedByNavigation.Username,
                FullName = application.ReviewedByNavigation.FullName,
                Email = application.ReviewedByNavigation.Email,
                AvatarUrl = application.ReviewedByNavigation.AvatarUrl,
                Phone = application.ReviewedByNavigation.Phone,
                Roles = application.ReviewedByNavigation.UserRoles.Select(userRole => userRole.Role.Name).ToList()
            },
            AssignedRecruiterId = application.AssignedRecruiterId?.ToString(),
            AssignedRecruiterName = application.AssignedRecruiter?.FullName,
            AssignedRecruiterEmail = application.AssignedRecruiter?.Email,
            AssignedDepartmentHeadId = application.AssignedDepartmentHeadId?.ToString(),
            AssignedDepartmentHeadName = application.AssignedDepartmentHead?.FullName,
            AssignedDepartmentHeadEmail = application.AssignedDepartmentHead?.Email
        };
    }

    /// <summary>
    /// Maps manager review queue item.
    /// </summary>
    /// <param name="application">The <paramref name="application"/> value.</param>
    /// <returns>The operation result.</returns>
    private static ManagerReviewQueueItemDto MapManagerReviewQueueItem(Domain.Entities.Application application)
    {
        (double score, string recommendation) = BuildReviewScore(application);
        string fullName = application.User.FullName;
        string location = application.User.CandidateProfile?.Address;

        return new ManagerReviewQueueItemDto
        {
            ApplicationId = application.Id.ToString(),
            CandidateName = fullName,
            CandidateInitials = BuildInitials(fullName),
            CandidateAvatarUrl = application.User.AvatarUrl,
            CandidateLocation = string.IsNullOrWhiteSpace(location) ? "Location unavailable" : location,
            JobTitle = application.Job.Title,
            Score = score,
            Recommendation = recommendation,
            Status = application.Status.ToString(),
            AppliedAt = application.AppliedAt,
            CompletedInterviews = application.Interviews.Count(interview => interview.Status == InterviewStatus.Completed),
            TotalInterviews = application.Interviews.Count,
            AssignedRecruiterId = application.AssignedRecruiterId?.ToString(),
            AssignedRecruiterName = application.AssignedRecruiter?.FullName,
            AssignedDepartmentHeadId = application.AssignedDepartmentHeadId?.ToString(),
            AssignedDepartmentHeadName = application.AssignedDepartmentHead?.FullName
        };
    }

    /// <summary>
    /// Executes the static operation.
    /// </summary>
    /// <param name="Score">The <paramref name="Score"/> value.</param>
    /// <param name="application">The <paramref name="application"/> value.</param>
    private static (double Score, string Recommendation) BuildReviewScore(Domain.Entities.Application application)
    {
        CandidateProfile? profile = application.User.CandidateProfile;
        if (profile == null)
        {
            return (0, "Flagged");
        }

        Dictionary<string, decimal?> candidateSkillYears = new(StringComparer.OrdinalIgnoreCase);
        foreach (CandidateSkill candidateSkill in profile.CandidateSkills)
        {
            string? skillName = candidateSkill.Skill?.Name;
            if (!string.IsNullOrWhiteSpace(skillName))
            {
                candidateSkillYears[skillName] = candidateSkill.YearsOfExperience;
            }
        }

        List<JobSkill> requiredSkills = application.Job.JobSkills
            .Where(jobSkill => jobSkill.IsRequired)
            .ToList();
        List<JobSkill> niceToHaveSkills = application.Job.JobSkills
            .Where(jobSkill => !jobSkill.IsRequired)
            .ToList();

        double requiredSkillMatchRatio = requiredSkills.Count == 0
            ? 0
            : requiredSkills.Count(jobSkill => candidateSkillYears.ContainsKey(jobSkill.Skill.Name)) / (double)requiredSkills.Count;

        List<JobSkill> experienceDrivenRequirements = requiredSkills
            .Where(jobSkill => jobSkill.MinimumYearsOfExperience.HasValue && jobSkill.MinimumYearsOfExperience.Value > 0)
            .ToList();

        double skillExperienceMatchRatio = experienceDrivenRequirements.Count == 0
            ? 0
            : experienceDrivenRequirements
                .Average(jobSkill =>
                {
                    decimal candidateYears = candidateSkillYears.TryGetValue(jobSkill.Skill.Name, out decimal? yearsOfExperience)
                        ? yearsOfExperience ?? 0
                        : 0;
                    decimal requiredYears = jobSkill.MinimumYearsOfExperience ?? 0;
                    if (requiredYears <= 0)
                    {
                        return candidateYears > 0 ? 1d : 0d;
                    }

                    return (double)Math.Min(candidateYears / requiredYears, 1);
                });

        double niceToHaveMatchRatio = niceToHaveSkills.Count == 0
            ? 0
            : niceToHaveSkills.Count(jobSkill => candidateSkillYears.ContainsKey(jobSkill.Skill.Name)) / (double)niceToHaveSkills.Count;

        int projectCount = profile.Projects.Count;
        int experienceCount = LoadDocumentCount(profile.ExperienceEntriesJson);
        double projectMatchRatio = projectCount > 0
            ? 1
            : experienceCount > 0
                ? 0.5
                : 0;

        int educationCount = LoadDocumentCount(profile.EducationRecordsJson);
        int certificationCount = LoadDocumentCount(profile.CertificationRecordsJson);
        int languageCount = LoadDocumentCount(profile.LanguageRecordsJson);
        double educationAndCertificationRatio = 0;
        if (educationCount > 0)
        {
            educationAndCertificationRatio += 0.5;
        }

        if (certificationCount > 0 || languageCount > 0)
        {
            educationAndCertificationRatio += 0.5;
        }

        double completionRatio = (double)CalculateProfileCompletionScore(profile) / 100d;

        List<(double Ratio, double Weight)> components = [];
        if (requiredSkills.Count > 0)
        {
            components.Add((requiredSkillMatchRatio, 40));
        }

        if (experienceDrivenRequirements.Count > 0)
        {
            components.Add((skillExperienceMatchRatio, 25));
        }

        if (niceToHaveSkills.Count > 0)
        {
            components.Add((niceToHaveMatchRatio, 15));
        }

        components.Add((projectMatchRatio, 10));
        components.Add((educationAndCertificationRatio, 5));
        components.Add((completionRatio, 5));

        double totalWeight = components.Sum(component => component.Weight);
        double score = totalWeight == 0
            ? 0
            : Math.Round(components.Sum(component => component.Ratio * component.Weight) / totalWeight * 100, 1);
        score = Math.Min(Math.Max(score, 0), 100);

        string recommendation = score switch
        {
            >= 85 => "Strong Hire",
            >= 70 => "Hire",
            >= 50 => "Hold",
            _ => "Flagged"
        };

        return (score, recommendation);
    }

    /// <summary>
    /// Loads document count.
    /// </summary>
    /// <param name="jsonString">The <paramref name="jsonString"/> value.</param>
    /// <returns>The operation result.</returns>
    private static int LoadDocumentCount(string? jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return 0;
        }

        try
        {
            List<JsonElement>? parsed = JsonSerializer.Deserialize<List<JsonElement>>(jsonString);
            return parsed?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Calculates profile completion score.
    /// </summary>
    /// <param name="profile">The <paramref name="profile"/> value.</param>
    /// <returns>The operation result.</returns>
    private static decimal CalculateProfileCompletionScore(CandidateProfile profile)
    {
        decimal score = 0;

        if (!string.IsNullOrWhiteSpace(profile.User.FullName)
            && !string.IsNullOrWhiteSpace(profile.User.Email)
            && !string.IsNullOrWhiteSpace(profile.Address))
        {
            score += 20;
        }

        if (!string.IsNullOrWhiteSpace(profile.CurrentPosition) && !string.IsNullOrWhiteSpace(profile.Bio))
        {
            score += 15;
        }

        if (profile.CandidateSkills.Count > 0)
        {
            score += 20;
        }

        if (LoadDocumentCount(profile.ExperienceEntriesJson) > 0)
        {
            score += 15;
        }

        if (profile.Projects.Count > 0)
        {
            score += 10;
        }

        if (LoadDocumentCount(profile.EducationRecordsJson) > 0)
        {
            score += 10;
        }

        if (LoadDocumentCount(profile.CertificationRecordsJson) > 0 || LoadDocumentCount(profile.LanguageRecordsJson) > 0)
        {
            score += 5;
        }

        if (profile.Resumes.Any(item => item.IsCurrent) || !string.IsNullOrWhiteSpace(profile.ResumeUrl))
        {
            score += 5;
        }

        return Math.Min(score, 100);
    }

    /// <summary>
    /// Builds meta.
    /// </summary>
    /// <param name="page">The <paramref name="page"/> value.</param>
    /// <param name="pageSize">The <paramref name="pageSize"/> value.</param>
    /// <param name="total">The <paramref name="total"/> value.</param>
    /// <returns>The operation result.</returns>
    /// <summary>
    /// Parses application status.
    /// </summary>
    /// <param name="value">The <paramref name="value"/> value.</param>
    /// <returns>The operation result.</returns>
    private static ApplicationStatus? ParseApplicationStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "applied" or "pending" => ApplicationStatus.Applied,
            "screening" or "hrscreening" or "hr-screening" or "reviewing" or "under review" => ApplicationStatus.Screening,
            "managerreview" or "manager-review" => ApplicationStatus.ManagerReview,
            "interview" or "interviewscheduled" or "interview-scheduled" or "interviewing" => ApplicationStatus.Interview,
            "offer" or "waitingoffer" or "waiting-offer" or "offersent" or "offer-sent" or "offered" => ApplicationStatus.Offer,
            "hired" or "accepted" => ApplicationStatus.Hired,
            "rejected" => ApplicationStatus.Rejected,
            "offerdeclined" or "offer-declined" or "declined" => ApplicationStatus.OfferDeclined,
            "withdrawn" or "withdraw" => ApplicationStatus.Withdrawn,
            _ => null
        };
    }

    /// <summary>
    /// Extracts file name.
    /// </summary>
    /// <param name="resumeValue">The <paramref name="resumeValue"/> value.</param>
    /// <returns>The resulting string value.</returns>
    /// <summary>
    /// Builds reference code.
    /// </summary>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <returns>The resulting string value.</returns>
    private static string BuildReferenceCode(Guid applicationId)
    {
        string compactId = applicationId.ToString("N")[..8].ToUpperInvariant();
        return $"APP-{compactId}";
    }

    /// <summary>
    /// Retrieves current resume.
    /// </summary>
    /// <param name="profile">The <paramref name="profile"/> value.</param>
    /// <returns>The operation result.</returns>
    private static CandidateResume? GetCurrentResume(CandidateProfile profile)
    {
        CandidateResume? currentResume = profile.Resumes
            .OrderByDescending(item => item.Version)
            .FirstOrDefault(item => item.IsCurrent);

        if (currentResume != null)
        {
            return currentResume;
        }

        if (string.IsNullOrWhiteSpace(profile.ResumeUrl))
        {
            return null;
        }

        return new CandidateResume
        {
            Id = profile.Id,
            CandidateProfileId = profile.Id,
            FileName = StoredFileNameHelper.ExtractDisplayFileName(profile.ResumeUrl),
            StorageKey = profile.ResumeUrl,
            UploadDate = profile.User.UpdatedAt ?? profile.User.CreatedAt ?? DbDateTime.Now,
            Version = 1,
            IsCurrent = true
        };
    }

    /// <summary>
    /// Builds stage label.
    /// </summary>
    /// <param name="application">The <paramref name="application"/> value.</param>
    /// <returns>The resulting string value.</returns>
    private static string BuildStageLabel(Domain.Entities.Application application)
    {
        return application.Status switch
        {
            ApplicationStatus.Applied => "Applied",
            ApplicationStatus.Screening => "Screening",
            ApplicationStatus.ManagerReview => "Manager Review",
            ApplicationStatus.Interview => "Interview",
            ApplicationStatus.Offer => "Offer",
            ApplicationStatus.Hired => "Hired",
            ApplicationStatus.Rejected => "Rejected",
            ApplicationStatus.OfferDeclined => "Offer Declined",
            ApplicationStatus.Withdrawn => "Withdrawn",
            _ => application.Status.ToString()
        };
    }

    /// <summary>
    /// Maps candidate application status.
    /// </summary>
    /// <param name="application">The <paramref name="application"/> value.</param>
    /// <returns>The resulting string value.</returns>
    // Localized (Vietnamese) DISPLAY label for the candidate application status. This is used ONLY for
    // the presentation-layer StatusLabel field — the canonical English enum drives the Status field and
    // all frontend logic (KEEP-CANONICAL-STATUS contract).
    private static string MapCandidateApplicationStatus(Domain.Entities.Application application)
    {
        return application.Status switch
        {
            ApplicationStatus.Applied => "Mới nộp",
            ApplicationStatus.Screening => "HR đang sàng lọc",
            ApplicationStatus.ManagerReview => "Quản lý đang đánh giá",
            ApplicationStatus.Interview => "Phỏng vấn",
            ApplicationStatus.Offer => "Chờ phản hồi offer",
            ApplicationStatus.Hired => "Đã tuyển dụng",
            ApplicationStatus.Rejected => "Không phù hợp",
            ApplicationStatus.OfferDeclined => "Đã từ chối offer",
            ApplicationStatus.Withdrawn => "Đã rút đơn",
            _ => application.Status.ToString()
        };
    }

    /// <summary>
    /// Builds candidate next step.
    /// </summary>
    /// <param name="application">The <paramref name="application"/> value.</param>
    /// <returns>The resulting string value.</returns>
    private static string BuildCandidateNextStep(Domain.Entities.Application application)
    {
        return application.Status switch
        {
            ApplicationStatus.Applied =>
                "Hồ sơ đã được ghi nhận và đang chờ HR tiếp nhận.",

            ApplicationStatus.Screening =>
                "HR đang kiểm tra mức độ phù hợp của hồ sơ với vị trí.",

            ApplicationStatus.ManagerReview =>
                "Hồ sơ đang được quản lý chuyên môn đánh giá thêm.",

            ApplicationStatus.Interview =>
                "Bạn đã vào vòng phỏng vấn. Hãy theo dõi thông báo để cập nhật lịch.",

            ApplicationStatus.Offer =>
                "Bạn đã nhận offer. Vui lòng phản hồi trong thời gian quy định.",

            ApplicationStatus.Hired =>
                "Chúc mừng! Bạn đã chấp nhận đề nghị tuyển dụng và hoàn tất quy trình ứng tuyển.",

            ApplicationStatus.Rejected =>
                "Quy trình ứng tuyển cho vị trí này đã kết thúc. Cảm ơn bạn đã quan tâm đến cơ hội việc làm tại công ty.",

            ApplicationStatus.OfferDeclined =>
                "Bạn đã từ chối đề nghị tuyển dụng cho vị trí này.",

            ApplicationStatus.Withdrawn =>
                "Bạn đã rút đơn ứng tuyển. Bạn có thể ứng tuyển lại vị trí này bất cứ lúc nào.",

            _ =>
                "Trạng thái hồ sơ đang được cập nhật."
        };
    }

    /// <summary>
    /// Builds review next step.
    /// </summary>
    /// <param name="application">The <paramref name="application"/> value.</param>
    /// <returns>The resulting string value.</returns>
    private static string BuildReviewNextStep(Domain.Entities.Application application)
    {
        return application.Status switch
        {
            ApplicationStatus.Applied => "Move application into HR screening.",
            ApplicationStatus.Screening => "Advance to manager review or reject.",
            ApplicationStatus.ManagerReview => "Schedule interview or reject.",
            ApplicationStatus.Interview => "Complete interviews and decide next step.",
            ApplicationStatus.Offer => "Prepare, send, and track offer response.",
            ApplicationStatus.Hired => "Candidate accepted the offer.",
            ApplicationStatus.OfferDeclined => "Candidate declined the offer.",
            ApplicationStatus.Rejected => "Application closed.",
            ApplicationStatus.Withdrawn => "Candidate withdrew the application.",
            _ => "Continue workflow."
        };
    }

    /// <summary>
    /// Builds candidate available actions.
    /// </summary>
    /// <param name="application">The <paramref name="application"/> value.</param>
    /// <returns>The operation result.</returns>
    private static List<string> BuildCandidateAvailableActions(Domain.Entities.Application application)
    {
        List<string> actions = ["viewDetail"];

        if (ApplicationStatusWorkflow.CanCandidateWithdraw(application.Status))
        {
            actions.Add("withdraw");
        }

        if (ApplicationStatusWorkflow.CanCandidateRespondToOffer(application.Status))
        {
            actions.Add("acceptOffer");
            actions.Add("declineOffer");
        }

        return actions;
    }

    /// <summary>
    /// Builds initials.
    /// </summary>
    /// <param name="fullName">The <paramref name="fullName"/> value.</param>
    /// <returns>The resulting string value.</returns>
    private static string BuildInitials(string fullName)
    {
        return string.Concat(
            fullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(part => char.ToUpperInvariant(part[0])));
    }
}
