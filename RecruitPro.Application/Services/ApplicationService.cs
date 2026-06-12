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

namespace RecruitPro.Application.Services;

public class ApplicationService : IApplicationService
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly ICandidateProfileRepository _candidateProfileRepository;
    private readonly IJobRepository _jobRepository;
    private readonly IOfferRepository _offerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<ApplicationService> _logger;

    public ApplicationService(
        IApplicationRepository applicationRepository,
        ICandidateProfileRepository candidateProfileRepository,
        IJobRepository jobRepository,
        IOfferRepository offerRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        ILogger<ApplicationService> logger)
    {
        _applicationRepository = applicationRepository;
        _candidateProfileRepository = candidateProfileRepository;
        _jobRepository = jobRepository;
        _offerRepository = offerRepository;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<ApiResponse<ApplyJobScreenDto>> GetApplyScreenAsync(Guid userId, string jobId)
    {
        Job job = await GetJobAsync(jobId);
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        Domain.Entities.Application? existingApplication = await GetExistingApplicationAsync(userId, job.Id);
        ApplyJobEligibilityDto eligibility = BuildApplyEligibility(job, profile, existingApplication);

        ApplyJobResumeDto? resume = null;
        if (!string.IsNullOrWhiteSpace(profile.ResumeUrl))
        {
            resume = new ApplyJobResumeDto
            {
                ResumeId = profile.Id.ToString(),
                FileName = ExtractFileName(profile.ResumeUrl),
                FileUrl = await _fileStorage.GetPresignedUrlAsync(profile.ResumeUrl),
                UploadedAt = profile.User.UpdatedAt ?? profile.User.CreatedAt ?? DbDateTime.Now
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
                SalaryLabel = BuildSalaryLabel(job.SalaryMin, job.SalaryMax),
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

        Domain.Entities.Application application = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobId = job.Id,
            Status = ApplicationStatus.Pending,
            AppliedAt = DbDateTime.Now,
            CoverLetter = string.IsNullOrWhiteSpace(request.CoverLetter)
                ? null
                : request.CoverLetter.Trim()
        };

        await _unitOfWork.BeginTransactionAsync();
        await _applicationRepository.AddAsync(application);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();

        return ApiResponse<ApplyJobResponseDto>.Created(new ApplyJobResponseDto
        {
            ApplicationId = application.Id.ToString(),
            Status = application.Status.ToString()
        }, "Application submitted successfully");
    }

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
            Score = null
        }).ToList();

        return ApiResponse<IReadOnlyList<RecentJobApplicationDto>>.Ok(items);
    }

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
                AvailableActions = application.Status == ApplicationStatus.Accepted
                    ? ["viewDetail"]
                    : application.Status == ApplicationStatus.ManagerReview && application.Offer?.Status == OfferStatus.Sent
                        ? ["viewDetail", "acceptOffer", "withdraw"]
                        : ["viewDetail", "withdraw"]
            }).ToList();

        return ApiResponse<CandidateApplicationsResponseDto>.Ok(new CandidateApplicationsResponseDto
        {
            Items = items,
            Meta = BuildMeta(page, pageSize, total),
            Summary = new CandidateApplicationSummaryDto
            {
                Total = total,
                Active = query.Count(application => application.Status != ApplicationStatus.Rejected && application.Status != ApplicationStatus.Accepted),
                Closed = query.Count(application => application.Status == ApplicationStatus.Rejected || application.Status == ApplicationStatus.Accepted)
            }
        });
    }

    public async Task<ApiResponse<string>> WithdrawApplicationAsync(Guid userId, string applicationId)
    {
        Domain.Entities.Application application = await GetTrackedApplicationForCandidateAsync(userId, applicationId);
        application.Status = ApplicationStatus.Rejected;

        await _unitOfWork.BeginTransactionAsync();
        await _applicationRepository.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();
        return ApiResponse<string>.Ok("Application withdrawn successfully", "Application withdrawn successfully");
    }

    public async Task<ApiResponse<string>> AcceptOfferAsync(Guid userId, string applicationId)
    {
        Domain.Entities.Application application = await GetTrackedApplicationForCandidateAsync(userId, applicationId);

        if (application.Status != ApplicationStatus.ManagerReview && application.Status != ApplicationStatus.Accepted)
        {
            return ApiResponse<string>.BadRequest("This application is not ready for offer acceptance.");
        }

        if (application.Status == ApplicationStatus.ManagerReview && application.Offer?.Status != OfferStatus.Sent)
        {
            return ApiResponse<string>.BadRequest("An offer has not been sent for this application yet.");
        }

        application.Status = ApplicationStatus.Accepted;
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

        return ApiResponse<string>.Ok("Offer accepted successfully", "Offer accepted successfully");
    }

    public async Task<ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>> GetHrApplicationsAsync(int page, int pageSize, string? keyword, string? department, string? status)
    {
        ApplicationStatus? parsedStatus = ParseApplicationStatus(status);
        (IReadOnlyList<Domain.Entities.Application> applications, int total) = await _applicationRepository.GetPagedAsync(page, pageSize, keyword, department, parsedStatus);

        return ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>.Ok(new PaginatedResponseDto<ApplicationListItemDto>
        {
            Items = applications.Select(MapApplicationToDto).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = total
        });
    }

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
            Meta = BuildMeta(safePage, safePageSize, queueItems.Count),
            Summary = new ManagerReviewQueueSummaryDto
            {
                PendingFinalApprovals = queueItems.Count,
                RecommendedCount = recommendedCount,
                FlaggedCount = Math.Max(queueItems.Count - recommendedCount, 0),
                AverageScore = queueItems.Count == 0 ? 0 : Math.Round(queueItems.Average(item => item.Score), 1)
            }
        });
    }

    public async Task<ApiResponse<ApplicationReviewDetailDto>> GetApplicationReviewDetailAsync(string applicationId)
    {
        if (!Guid.TryParse(applicationId, out Guid applicationGuid))
        {
            return ApiResponse<ApplicationReviewDetailDto>.NotFound("Application not found.");
        }

        Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(applicationGuid);
        if (application == null)
        {
            return ApiResponse<ApplicationReviewDetailDto>.NotFound("Application not found.");
        }

        return ApiResponse<ApplicationReviewDetailDto>.Ok(MapApplicationToReviewDetailDto(application));
    }

    public async Task<ApiResponse<ApplicationReviewDetailDto>> UpdateApplicationDecisionAsync(
        string applicationId,
        Guid? reviewerId,
        UpdateApplicationDecisionRequest request)
    {
        if (!Guid.TryParse(applicationId, out Guid applicationGuid))
        {
            return ApiResponse<ApplicationReviewDetailDto>.NotFound("Application not found.");
        }

        Domain.Entities.Application? application = await _applicationRepository.GetTrackedByIdAsync(applicationGuid);
        if (application == null)
        {
            return ApiResponse<ApplicationReviewDetailDto>.NotFound("Application not found.");
        }

        string normalizedDecision = request.Decision.Trim().ToLowerInvariant();
        if (application.Status == ApplicationStatus.Accepted && normalizedDecision != "hire")
        {
            return ApiResponse<ApplicationReviewDetailDto>.BadRequest("Accepted applications cannot be moved back to hold or rejected.");
        }

        application.Status = normalizedDecision switch
        {
            "hire" => ApplicationStatus.ManagerReview,
            "hold" => ApplicationStatus.Reviewing,
            "reject" => ApplicationStatus.Rejected,
            _ => application.Status
        };

        if (reviewerId.HasValue)
        {
            application.ReviewedBy = reviewerId.Value;
        }

        await _unitOfWork.BeginTransactionAsync();
        await _applicationRepository.UpdateAsync(application);
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitAsync();

        Domain.Entities.Application? refreshedApplication = await _applicationRepository.GetByIdAsync(applicationGuid);
        if (refreshedApplication == null)
        {
            return ApiResponse<ApplicationReviewDetailDto>.NotFound("Application not found.");
        }

        string message = normalizedDecision switch
        {
            "hire" => "Candidate approved for offer workflow.",
            "hold" => "Application moved back to review.",
            "reject" => "Application rejected successfully.",
            _ => "Application updated successfully."
        };

        return ApiResponse<ApplicationReviewDetailDto>.Ok(MapApplicationToReviewDetailDto(refreshedApplication), message);
    }

    public async Task<ApiResponse<ResumeFileResponseDto>> GetApplicationCvAsync(string applicationId)
    {
        if (!Guid.TryParse(applicationId, out Guid applicationGuid))
        {
            return ApiResponse<ResumeFileResponseDto>.NotFound("Application not found.");
        }

        Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(applicationGuid);
        if (application == null)
        {
            return ApiResponse<ResumeFileResponseDto>.NotFound("Application not found.");
        }

        string? resumeObjectKey = application.User.CandidateProfile?.ResumeUrl;
        if (string.IsNullOrWhiteSpace(resumeObjectKey))
        {
            return ApiResponse<ResumeFileResponseDto>.NotFound("Resume not found.");
        }

        string presignedUrl = await _fileStorage.GetPresignedUrlAsync(resumeObjectKey);
        _logger.LogInformation(
            "Generated resume download URL for application {ApplicationId} and candidate {CandidateId}.",
            application.Id,
            application.UserId);

        return ApiResponse<ResumeFileResponseDto>.Ok(new ResumeFileResponseDto
        {
            ResumeId = application.UserId.ToString(),
            FileName = ExtractFileName(resumeObjectKey),
            FileUrl = presignedUrl
        });
    }

    public async Task<ApiResponse<string>> SendApplicationEmailAsync(string applicationId, SendApplicationEmailRequest request)
    {
        if (!Guid.TryParse(applicationId, out Guid applicationGuid))
        {
            return ApiResponse<string>.NotFound("Application not found.");
        }

        Domain.Entities.Application? application = await _applicationRepository.GetByIdAsync(applicationGuid);
        if (application == null)
        {
            return ApiResponse<string>.NotFound("Application not found.");
        }

        string recipient = application.User.Email;
        string normalizedTemplate = string.IsNullOrWhiteSpace(request.TemplateType) ? "General" : request.TemplateType.Trim();
        string effectiveSubject = string.IsNullOrWhiteSpace(request.Subject) ? $"{normalizedTemplate} - {application.Job.Title}" : request.Subject.Trim();
        string effectiveBody = string.IsNullOrWhiteSpace(request.Body)
            ? $"Prepared {normalizedTemplate} email for {recipient} regarding {application.Job.Title}."
            : request.Body.Trim();

        return ApiResponse<string>.Ok($"{effectiveSubject}: {effectiveBody}", $"Email prepared for {recipient}");
    }

    private async Task<Domain.Entities.Application?> GetExistingApplicationAsync(Guid userId, Guid jobId)
    {
        IReadOnlyList<Domain.Entities.Application> existingApplications = await _applicationRepository.GetByUserIdAsync(userId);
        return existingApplications.FirstOrDefault(application => application.JobId == jobId);
    }

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

        if (string.IsNullOrWhiteSpace(profile.ResumeUrl))
        {
            blockers.Add("Please upload your latest resume before applying.");
        }

        if (existingApplication != null)
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
                : existingApplication != null
                    ? "Track the latest status of this application from My Applications."
                    : "Complete the missing requirements before submitting your application."
        };
    }

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

    private async Task<CandidateProfile> GetProfileEntityAsync(Guid userId)
    {
        CandidateProfile? profile = await _candidateProfileRepository.GetByUserIdAsync(userId);
        if (profile == null)
        {
            throw new NotFoundException("Candidate profile not found.");
        }

        return profile;
    }

    private async Task<Domain.Entities.Application> GetTrackedApplicationForCandidateAsync(Guid userId, string applicationId)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        if (!Guid.TryParse(applicationId, out Guid applicationGuid))
        {
            throw new NotFoundException("Application not found.");
        }

        Domain.Entities.Application? application = await _applicationRepository.GetTrackedByIdAsync(applicationGuid);
        if (application == null || application.UserId != profile.UserId)
        {
            throw new NotFoundException("Application not found.");
        }

        return application;
    }

    private static ApplicationListItemDto MapApplicationToDto(Domain.Entities.Application application)
    {
        return new ApplicationListItemDto
        {
            Id = application.Id.ToString(),
            Candidate = new ApplicationCandidateSummaryDto
            {
                Id = application.UserId.ToString(),
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
                FullName = application.ReviewedByNavigation.FullName,
                Email = application.ReviewedByNavigation.Email,
                AvatarUrl = application.ReviewedByNavigation.AvatarUrl,
                Phone = application.ReviewedByNavigation.Phone,
                Roles = application.ReviewedByNavigation.UserRoles.Select(userRole => userRole.Role.Name).ToList()
            },
            NextStep = application.Interviews.Any() ? "Interview scheduled" : "In review"
        };
    }

    private static ApplicationReviewDetailDto MapApplicationToReviewDetailDto(Domain.Entities.Application application)
    {
        List<string> candidateSkills = application.User.CandidateProfile?.Skills
            .Select(skill => skill.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList() ?? [];

        List<string> requiredSkills = application.Job.JobSkills
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
                ApplicationStatus.ManagerReview when application.Offer?.Status == OfferStatus.Sent => "Offer sent to candidate",
                ApplicationStatus.ManagerReview when application.Offer != null => "Offer draft in progress",
                ApplicationStatus.ManagerReview => "Ready for offer preparation",
                ApplicationStatus.Rejected => "Application closed",
                ApplicationStatus.Accepted => "Candidate accepted offer",
                _ => orderedInterviews.Any()
                    ? "Awaiting final manager decision"
                    : "Continue application review"
            },
            Candidate = new ApplicationReviewCandidateDto
            {
                Id = application.UserId.ToString(),
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
                FullName = application.ReviewedByNavigation.FullName,
                Email = application.ReviewedByNavigation.Email,
                AvatarUrl = application.ReviewedByNavigation.AvatarUrl,
                Phone = application.ReviewedByNavigation.Phone,
                Roles = application.ReviewedByNavigation.UserRoles.Select(userRole => userRole.Role.Name).ToList()
            }
        };
    }

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

    private static (double Score, string Recommendation) BuildReviewScore(Domain.Entities.Application application)
    {
        List<string> candidateSkills = application.User.CandidateProfile?.Skills
            .Select(skill => skill.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        List<string> requiredSkills = application.Job.JobSkills
            .Select(jobSkill => jobSkill.Skill.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        int matchedSkillCount = requiredSkills.Count(requiredSkill =>
            candidateSkills.Any(candidateSkill => candidateSkill.Equals(requiredSkill, StringComparison.OrdinalIgnoreCase)));

        double skillRatio = requiredSkills.Count == 0 ? 1 : matchedSkillCount / (double)requiredSkills.Count;
        int totalInterviews = application.Interviews.Count;
        int completedInterviews = application.Interviews.Count(interview => interview.Status == InterviewStatus.Completed);
        double interviewCompletionRatio = totalInterviews == 0 ? 0 : completedInterviews / (double)totalInterviews;
        double noteCoverageRatio = totalInterviews == 0
            ? 0
            : application.Interviews.Count(interview => !string.IsNullOrWhiteSpace(interview.Notes)) / (double)totalInterviews;

        double score = Math.Round(((skillRatio * 3d) + (interviewCompletionRatio * 1.25d) + (noteCoverageRatio * 0.75d)), 1);
        score = Math.Min(Math.Max(score, 0), 5);

        string recommendation = score switch
        {
            >= 4.5 => "Strong Hire",
            >= 4.0 => "Hire",
            >= 3.0 => "Hold",
            _ => "Flagged"
        };

        return (score, recommendation);
    }

    private static ApiEnvelopeMeta BuildMeta(int page, int pageSize, int total)
    {
        return new ApiEnvelopeMeta
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    private static string BuildSalaryLabel(decimal? salaryMin, decimal? salaryMax)
    {
        if (!salaryMin.HasValue && !salaryMax.HasValue)
        {
            return "Negotiable";
        }

        decimal effectiveMin = salaryMin ?? salaryMax ?? 0;
        decimal effectiveMax = salaryMax ?? salaryMin ?? 0;
        return $"{effectiveMin:N0} - {effectiveMax:N0} USD";
    }

    private static ApplicationStatus? ParseApplicationStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "pending" => ApplicationStatus.Pending,
            "reviewing" or "under review" => ApplicationStatus.Reviewing,
            "interviewing" => ApplicationStatus.Interviewing,
            "managerreview" or "manager-review" => ApplicationStatus.ManagerReview,
            "accepted" => ApplicationStatus.Accepted,
            "rejected" => ApplicationStatus.Rejected,
            _ => null
        };
    }

    private static string ExtractFileName(string resumeValue)
    {
        string fileName = Path.GetFileName(
            Uri.TryCreate(resumeValue, UriKind.Absolute, out Uri? uri)
                ? uri.AbsolutePath
                : resumeValue);

        int separatorIndex = fileName.IndexOf('_');
        return separatorIndex >= 0 && separatorIndex < fileName.Length - 1
            ? fileName[(separatorIndex + 1)..]
            : fileName;
    }

    private static string BuildReferenceCode(Guid applicationId)
    {
        string compactId = applicationId.ToString("N")[..8].ToUpperInvariant();
        return $"APP-{compactId}";
    }

    private static string BuildStageLabel(Domain.Entities.Application application)
    {
        return application.Status switch
        {
            ApplicationStatus.ManagerReview when application.Offer?.Status == OfferStatus.Sent => "Offer Sent",
            ApplicationStatus.ManagerReview => "Offer Pending",
            ApplicationStatus.Accepted => "Hired",
            ApplicationStatus.Rejected => "Closed",
            _ when application.Interviews.Any() => "Final Review",
            _ => "Application Review"
        };
    }

    private static string MapCandidateApplicationStatus(Domain.Entities.Application application)
    {
        return application.Status switch
        {
            ApplicationStatus.Pending => "New",
            ApplicationStatus.Reviewing => "Under Review",
            ApplicationStatus.Interviewing => "Interviewing",
            ApplicationStatus.ManagerReview when application.Offer?.Status == OfferStatus.Sent => "Offered",
            ApplicationStatus.ManagerReview => "Final Review",
            ApplicationStatus.Accepted => "Accepted",
            ApplicationStatus.Rejected => "Rejected",
            _ => application.Status.ToString()
        };
    }

    private static string BuildCandidateNextStep(Domain.Entities.Application application)
    {
        return application.Status switch
        {
            ApplicationStatus.ManagerReview when application.Offer?.Status == OfferStatus.Sent => "Review and respond to your offer package",
            ApplicationStatus.ManagerReview => "HR is preparing your offer package",
            _ when application.Interviews.Any() => "Upcoming interview",
            _ => "Awaiting review"
        };
    }

    private static string BuildInitials(string fullName)
    {
        return string.Concat(
            fullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(part => char.ToUpperInvariant(part[0])));
    }
}
