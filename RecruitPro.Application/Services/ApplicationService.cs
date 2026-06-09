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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<ApplicationService> _logger;

    public ApplicationService(
        IApplicationRepository applicationRepository,
        ICandidateProfileRepository candidateProfileRepository,
        IJobRepository jobRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        ILogger<ApplicationService> logger)
    {
        _applicationRepository = applicationRepository;
        _candidateProfileRepository = candidateProfileRepository;
        _jobRepository = jobRepository;
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<ApiResponse<ApplyJobResponseDto>> ApplyAsync(Guid userId, string jobId, ApplyJobRequest request)
    {
        Job job = await GetJobAsync(jobId);
        CandidateProfile? profile = await _candidateProfileRepository.GetByUserIdAsync(userId);
        if (profile == null)
        {
            throw new NotFoundException("Candidate profile not found.");
        }

        if (await _applicationRepository.CandidateAlreadyAppliedAsync(userId, job.Id))
        {
            return ApiResponse<ApplyJobResponseDto>.BadRequest("Candidate already applied for this job.");
        }

        Domain.Entities.Application application = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobId = job.Id,
            Status = ApplicationStatus.Pending,
            AppliedAt = DbDateTime.Now
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
                Status = application.Status.ToString(),
                NextStep = application.Interviews.Any() ? "Upcoming interview" : "Awaiting review",
                AvailableActions = application.Status == ApplicationStatus.Accepted
                    ? ["viewDetail"]
                    : application.Status == ApplicationStatus.ManagerReview
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

        application.Status = ApplicationStatus.Accepted;
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
}
