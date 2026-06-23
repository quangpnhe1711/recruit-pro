using RecruitPro.Application.DTOs.Request.Applications;
using Microsoft.Extensions.Logging;
using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Common;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Domain.Workflows;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace RecruitPro.Application.Services;

public class ApplicationService : IApplicationService
{
    private const string ScoreStatusPendingSemantic = "PendingSemantic";
    private readonly IApplicationRepository _applicationRepository;
    private readonly ICandidateProfileRepository _candidateProfileRepository;
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
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        Domain.Entities.Application? existingApplication = await GetExistingApplicationAsync(userId, job.Id);
        ApplyJobEligibilityDto eligibility = BuildApplyEligibility(job, profile, existingApplication);

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
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        Domain.Entities.Application? existingApplication = await GetExistingApplicationAsync(userId, job.Id);
        ApplyJobEligibilityDto eligibility = BuildApplyEligibility(job, profile, existingApplication);
        if (!eligibility.CanApply)
        {
            string message = eligibility.AlreadyApplied
                ? "Candidate already applied for this job."
                : eligibility.Blockers.FirstOrDefault() ?? "This job cannot be applied for right now.";
            return ApiResponse<ApplyJobResponseDto>.BadRequest(message);
        }

        decimal ruleScore = CalculateRuleScore(profile, job);
        Domain.Entities.Application application = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobId = job.Id,
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
        application.User = profile.User;
        application.Job = job;

        await _unitOfWork.BeginTransactionAsync();
        await _applicationRepository.AddAsync(application);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();
        await _notificationEventService.PublishNewApplicationReceivedAsync(application);
        await _semanticProcessingQueue.EnqueueAsync(application.Id);

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
        CandidateProfile profile = await GetProfileEntityAsync(userId);
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
                Status = MapCandidateApplicationStatus(application),
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
            return ApiResponse<string>.BadRequest("This application can no longer be withdrawn.");
        }

        application.Status = ApplicationStatus.Rejected;

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

        if (!ApplicationStatusWorkflow.CanCandidateRespondToOffer(application.Status)
            && application.Status != ApplicationStatus.Hired)
        {
            return ApiResponse<string>.BadRequest("This application is not ready for offer acceptance.");
        }

        if (application.Status == ApplicationStatus.Offer && application.Offer?.Status != OfferStatus.Sent)
        {
            return ApiResponse<string>.BadRequest("An offer has not been sent for this application yet.");
        }

        application.Status = ApplicationStatus.Hired;
        ApplicationOffer? offer = await _offerRepository.GetTrackedByApplicationIdAsync(application.Id);
        if (offer != null)
        {
            offer.Status = OfferStatus.Accepted;
            offer.UpdatedAt = DbDateTime.Now;
            await _offerRepository.UpdateAsync(offer);
        }

        await _unitOfWork.BeginTransactionAsync();
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
            return ApiResponse<string>.BadRequest("This application is not waiting for an offer response.");
        }

        ApplicationOffer? offer = await _offerRepository.GetTrackedByApplicationIdAsync(application.Id);
        if (offer?.Status != OfferStatus.Sent)
        {
            return ApiResponse<string>.BadRequest("An offer has not been sent for this application yet.");
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
            return ApiResponse<ApplicationReviewDetailDto>.BadRequest("Target application status is invalid.");
        }

        if (!ApplicationStatusWorkflow.CanTransition(application.Status, targetStatus.Value))
        {
            return ApiResponse<ApplicationReviewDetailDto>.BadRequest(
                $"Invalid transition from {application.Status} to {targetStatus.Value}.");
        }

        ApplicationStatus previousStatus = application.Status;
        application.Status = targetStatus.Value;

        if (reviewerId.HasValue)
        {
            application.ReviewedBy = reviewerId.Value;
        }

        await _unitOfWork.BeginTransactionAsync();
        await _applicationRepository.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();
        await _notificationEventService.PublishApplicationStatusChangedAsync(application, previousStatus);

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
        return existingApplications.FirstOrDefault(application => application.JobId == jobId);
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
        Domain.Entities.Application? existingApplication)
    {
        List<string> blockers = [];

        if (job.Status != JobStatus.Approved)
        {
            blockers.Add("This job posting is not accepting new applications.");
        }

        if (job.Deadline.HasValue && job.Deadline.Value < DbDateTime.Now)
        {
            blockers.Add("The application deadline for this job has passed.");
        }

        if (string.IsNullOrWhiteSpace(profile.User.FullName) || string.IsNullOrWhiteSpace(profile.User.Email))
        {
            blockers.Add("Your profile is missing required contact information.");
        }

        if (GetCurrentResume(profile) == null)
        {
            blockers.Add("Please upload your latest resume before applying.");
        }

        if (existingApplication != null && !ApplicationStatusWorkflow.IsClosed(existingApplication.Status))
        {
            blockers.Add("You have already applied for this job.");
        }

        return new ApplyJobEligibilityDto
        {
            CanApply = blockers.Count == 0,
            AlreadyApplied = existingApplication != null,
            ExistingApplicationId = existingApplication?.Id.ToString(),
            ExistingApplicationStatus = existingApplication?.Status.ToString(),
            Blockers = blockers,
            GuidanceMessage = blockers.Count == 0
                ? "Your application will be submitted to the recruitment team for review."
                : existingApplication != null && !ApplicationStatusWorkflow.IsClosed(existingApplication.Status)
                    ? "Track the latest status of this application from My Applications."
                    : "Complete the missing requirements before submitting your application."
        };
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

    /// <summary>
    /// Retrieves tracked application for candidate.
    /// </summary>
    /// <param name="userId">The <paramref name="userId"/> value.</param>
    /// <param name="applicationId">The <paramref name="applicationId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="NotFoundException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    private async Task<Domain.Entities.Application> GetTrackedApplicationForCandidateAsync(Guid userId, string applicationId)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
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
            }
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
            TotalInterviews = application.Interviews.Count
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
            _ => application.Status.ToString()
        };
    }

    /// <summary>
    /// Maps candidate application status.
    /// </summary>
    /// <param name="application">The <paramref name="application"/> value.</param>
    /// <returns>The resulting string value.</returns>
    private static string MapCandidateApplicationStatus(Domain.Entities.Application application)
    {
        return application.Status switch
        {
            ApplicationStatus.Applied => "Đã ứng tuyển",
            ApplicationStatus.Screening => "Đang sàng lọc hồ sơ",
            ApplicationStatus.ManagerReview => "Đang đánh giá",
            ApplicationStatus.Interview => "Đã lên lịch phỏng vấn",
            ApplicationStatus.Offer => "Đã gửi đề nghị tuyển dụng",
            ApplicationStatus.Hired => "Đã tuyển dụng",
            ApplicationStatus.Rejected => "Không phù hợp",
            ApplicationStatus.OfferDeclined => "Đã từ chối đề nghị",
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
                "Hồ sơ của bạn đã được ghi nhận và đang chờ xem xét.",

            ApplicationStatus.Screening =>
                "Hồ sơ của bạn đang được đội ngũ tuyển dụng đánh giá.",

            ApplicationStatus.ManagerReview =>
                "Hồ sơ của bạn đang được quản lý tuyển dụng xem xét.",

            ApplicationStatus.Interview =>
                "Bạn đã được chọn vào vòng phỏng vấn. Vui lòng theo dõi thông báo để cập nhật lịch phỏng vấn.",

            ApplicationStatus.Offer =>
                "Bạn đã nhận được đề nghị tuyển dụng. Vui lòng xem chi tiết và phản hồi trong thời gian quy định.",

            ApplicationStatus.Hired =>
                "Chúc mừng! Bạn đã chấp nhận đề nghị tuyển dụng và hoàn tất quy trình ứng tuyển.",

            ApplicationStatus.Rejected =>
                "Quy trình ứng tuyển cho vị trí này đã kết thúc. Cảm ơn bạn đã quan tâm đến cơ hội việc làm tại công ty.",

            ApplicationStatus.OfferDeclined =>
                "Bạn đã từ chối đề nghị tuyển dụng cho vị trí này.",

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
