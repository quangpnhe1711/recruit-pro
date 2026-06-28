using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.Logging;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Request.Departments;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Services;

public class JobService : IJobService
{
    private readonly IJobRepository _jobRepository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly ISkillRepository _skillRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISemanticDiscoveryService _semanticDiscoveryService;
    private readonly INotificationEventService _notificationEventService;
    private readonly ILogger<JobService> _logger;
    private readonly IMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the JobService class.
    /// </summary>
    /// <param name="jobRepository">The <paramref name="jobRepository"/> value.</param>
    /// <param name="applicationRepository">The <paramref name="applicationRepository"/> value.</param>
    /// <param name="skillRepository">The <paramref name="skillRepository"/> value.</param>
    /// <param name="unitOfWork">The <paramref name="unitOfWork"/> value.</param>
    /// <param name="semanticDiscoveryService">The <paramref name="semanticDiscoveryService"/> value.</param>
    /// <param name="mapper">The <paramref name="mapper"/> value.</param>
    public JobService(
        IJobRepository jobRepository,
        IApplicationRepository applicationRepository,
        ISkillRepository skillRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ISemanticDiscoveryService semanticDiscoveryService,
        INotificationEventService notificationEventService,
        ILogger<JobService> logger,
        IMapper mapper)
    {
        _jobRepository = jobRepository;
        _applicationRepository = applicationRepository;
        _skillRepository = skillRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _semanticDiscoveryService = semanticDiscoveryService;
        _notificationEventService = notificationEventService;
        _logger = logger;
        _mapper = mapper;
    }

    /// <summary>
    /// Retrieves jobs.
    /// </summary>
    /// <param name="currentPage">The <paramref name="currentPage"/> value.</param>
    /// <param name="pageSize">The <paramref name="pageSize"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<JobsListingResponseDto>> GetJobsAsync(int currentPage = 1, int pageSize = 10)
    {
        (IReadOnlyList<Job> jobs, int total) = await _jobRepository.GetApprovedPagedAsync(currentPage, pageSize);
        List<JobCardDto> jobCards = _mapper.Map<List<JobCardDto>>(jobs);

        return ApiResponse<JobsListingResponseDto>.Ok(new JobsListingResponseDto
        {
            Jobs = jobCards,
            Total = total,
            Page = currentPage,
            Limit = pageSize
        });
    }

    /// <summary>
    /// Executes the search jobs operation.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<JobSearchResponseDto>> SearchJobsAsync(JobQueryRequest request)
    {
        (IReadOnlyList<Job> jobs, int total) = await _jobRepository.SearchApprovedAsync(
            request.Keyword,
            request.EmploymentTypes,
            request.Skills,
            request.SortBy,
            request.Page,
            request.PageSize);

        return ApiResponse<JobSearchResponseDto>.Ok(new JobSearchResponseDto
        {
            Items = jobs.Select(MapJobListItem).ToList(),
            Meta = PaginationMetaBuilder.Build(request.Page, request.PageSize, total)
        });
    }

    /// <summary>
    /// Retrieves filters.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<JobFiltersResponseDto>> GetFiltersAsync()
    {
        IReadOnlyList<string> skills = await _jobRepository.GetAllSkillNamesAsync();
        return ApiResponse<JobFiltersResponseDto>.Ok(new JobFiltersResponseDto
        {
            SalaryRanges =
            [
                new SalaryRangeDto { Label = "15.000.000 - 30.000.000 VNĐ", Min = 15000000, Max = 30000000 },
                new SalaryRangeDto { Label = "30.000.000 - 50.000.000 VNĐ", Min = 30000000, Max = 50000000 },
                new SalaryRangeDto { Label = "50.000.000 - 80.000.000 VNĐ", Min = 50000000, Max = 80000000 },
                new SalaryRangeDto { Label = "80.000.000+ VNĐ", Min = 80000000, Max = null }
            ],
            EmploymentTypes = ["Full-time", "Contract", "Internship", "Part-time"],
            Skills = skills.ToList()
        });
    }

    /// <summary>
    /// Retrieves departments.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<IReadOnlyList<DepartmentResponseDto>>> GetDepartmentsAsync()
    {
        IReadOnlyList<Department> departments = await _jobRepository.GetDepartmentsAsync();
        return ApiResponse<IReadOnlyList<DepartmentResponseDto>>.Ok(
            departments.Select(MapDepartment).ToList());
    }

    /// <summary>
    /// Retrieves a single department with its head (Phase 2/3).
    /// </summary>
    public async Task<ApiResponse<DepartmentResponseDto>> GetDepartmentAsync(string departmentId)
    {
        if (!Guid.TryParse(departmentId, out Guid departmentGuid))
        {
            return ApiResponse<DepartmentResponseDto>.NotFound("Department not found.", errorCode: ErrorCodes.DepartmentNotFound);
        }

        Department? department = await _jobRepository.GetDepartmentByIdAsync(departmentGuid);
        if (department == null)
        {
            return ApiResponse<DepartmentResponseDto>.NotFound("Department not found.", errorCode: ErrorCodes.DepartmentNotFound);
        }

        return ApiResponse<DepartmentResponseDto>.Ok(MapDepartment(department));
    }

    /// <summary>
    /// Updates a department's head (BR-OWN-001) and optional name/description. The head must be an
    /// existing user with the HeadDepartment or SystemAdmin role (no IsActive flag exists yet, so
    /// "active" = "exists" — role-by-existence validation; see docs follow-up).
    /// </summary>
    public async Task<ApiResponse<DepartmentResponseDto>> UpdateDepartmentAsync(string departmentId, UpdateDepartmentRequest request)
    {
        if (!Guid.TryParse(departmentId, out Guid departmentGuid))
        {
            return ApiResponse<DepartmentResponseDto>.NotFound("Department not found.", errorCode: ErrorCodes.DepartmentNotFound);
        }

        Department? department = await _jobRepository.GetTrackedDepartmentByIdAsync(departmentGuid);
        if (department == null)
        {
            return ApiResponse<DepartmentResponseDto>.NotFound("Department not found.", errorCode: ErrorCodes.DepartmentNotFound);
        }

        if (!string.IsNullOrWhiteSpace(request.HeadUserId))
        {
            if (!Guid.TryParse(request.HeadUserId, out Guid headUserGuid))
            {
                return ApiResponse<DepartmentResponseDto>.UnprocessableEntity(
                    "The selected department head is not a valid user.", errorCode: ErrorCodes.InvalidDepartmentHead);
            }

            User? headUser = await _userRepository.GetByIdAsync(headUserGuid);
            if (headUser == null || !UserHasAnyRole(headUser, RoleNames.HeadDepartment, RoleNames.SystemAdmin))
            {
                return ApiResponse<DepartmentResponseDto>.UnprocessableEntity(
                    "The selected department head must be an existing user with the HeadDepartment role.",
                    errorCode: ErrorCodes.InvalidDepartmentHead);
            }

            department.HeadUserId = headUser.Id;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            department.Name = request.Name.Trim();
        }

        if (request.Description != null)
        {
            department.Description = request.Description;
        }

        await _jobRepository.UpdateDepartmentAsync(department);
        await _unitOfWork.SaveChangesAsync();

        Department refreshed = await _jobRepository.GetDepartmentByIdAsync(departmentGuid) ?? department;
        return ApiResponse<DepartmentResponseDto>.Ok(MapDepartment(refreshed), "Department updated.");
    }

    /// <summary>
    /// Retrieves skills.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<IReadOnlyList<SkillLookupDto>>> GetSkillsAsync()
    {
        IReadOnlyList<Skill> skills = await _jobRepository.GetSkillsAsync();
        return ApiResponse<IReadOnlyList<SkillLookupDto>>.Ok(
            skills.Select(skill => new SkillLookupDto
            {
                Id = skill.Id.ToString(),
                Name = skill.Name
            }).ToList());
    }

    /// <summary>
    /// Retrieves job detail.
    /// </summary>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<JobDetailResponseDto>> GetJobDetailAsync(string jobId)
    {
        Job job = await GetJobAsync(jobId);
        return ApiResponse<JobDetailResponseDto>.Ok(MapLegacyJobDetail(job));
    }

    /// <summary>
    /// Retrieves job screen detail.
    /// </summary>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<JobDetailScreenDto>> GetJobScreenDetailAsync(string jobId)
    {
        Job job = await GetJobAsync(jobId);
        IReadOnlyList<Domain.Entities.Application> applications = await _applicationRepository.GetAllByJobIdAsync(job.Id);

        return ApiResponse<JobDetailScreenDto>.Ok(new JobDetailScreenDto
        {
            Id = job.Id.ToString(),
            Title = job.Title,
            Location = $"{job.Location} ({job.WorkMode})",
            PostedAt = job.CreatedAt,
            Status = job.Status.ToString(),
            SalaryRange = new SalaryRangeDto
            {
                Min = job.SalaryMin,
                Max = job.SalaryMax,
                Label = "VND"
            },
            SalaryLabel = CompensationLabelHelper.BuildSalaryLabel(job.SalaryMin, job.SalaryMax),
            Department = job.Department?.Name ?? string.Empty,
            JobType = $"{MapEmploymentType(job.EmploymentType)}, {job.WorkMode}",
            VacancyCount = job.VacancyCount,
            Description = ParseJsonArray(job.Description),
            Requirements = ParseJsonArray(job.Requirements),
            RequiredSkills = job.JobSkills
                .Where(jobSkill => jobSkill.IsRequired)
                .Select(MapJobSkill)
                .ToList(),
            NiceToHaveSkills = job.JobSkills
                .Where(jobSkill => !jobSkill.IsRequired)
                .Select(MapJobSkill)
                .ToList(),
            Skills = job.JobSkills.Select(MapJobSkill).ToList(),
            ApplicationSummary = new ApplicationSummaryDto
            {
                TotalApplications = applications.Count,
                Funnel =
                [
                    new FunnelCountDto { Label = "Applications", Count = applications.Count },
                    new FunnelCountDto { Label = "Screening", Count = applications.Count(application => application.Status == ApplicationStatus.Screening) },
                    new FunnelCountDto { Label = "Manager Review", Count = applications.Count(application => application.Status == ApplicationStatus.ManagerReview) },
                    new FunnelCountDto { Label = "Interview", Count = applications.Count(application => application.Status == ApplicationStatus.Interview) },
                    new FunnelCountDto { Label = "Offer", Count = applications.Count(application => application.Status == ApplicationStatus.Offer || application.Status == ApplicationStatus.Hired) }
                ]
            }
        });
    }

    /// <summary>
    /// Retrieves job statistics.
    /// </summary>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<HiringFunnelStatisticsDto>> GetJobStatisticsAsync(string jobId)
    {
        Job job = await GetJobAsync(jobId);
        IReadOnlyList<Domain.Entities.Application> applications = await _applicationRepository.GetAllByJobIdAsync(job.Id);

        return ApiResponse<HiringFunnelStatisticsDto>.Ok(new HiringFunnelStatisticsDto
        {
            Applied = applications.Count,
            Screening = applications.Count(application => application.Status == ApplicationStatus.Screening),
            Interview = applications.Count(application => application.Status == ApplicationStatus.Interview),
            Offer = applications.Count(application => application.Status == ApplicationStatus.Offer),
            Hired = applications.Count(application => application.Status == ApplicationStatus.Hired)
        });
    }

    // NOTE: the former unguarded `UpdateJobStatusAsync` was removed during Phase 2/3 hardening. The
    // `PATCH /api/jobs/{id}/status` endpoint now routes through `PatchJobAsync`, which enforces the
    // DepartmentHead/SystemAdmin approval guard (BR-OWN-003).

    /// <summary>
    /// Retrieves hr jobs.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <param name="currentUserId">The <paramref name="currentUserId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<HrJobsResponseDto>> GetHrJobsAsync(HrJobQueryRequest request, Guid currentUserId)
    {
        string? normalizedDepartment = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department;
        string? normalizedStatus = string.IsNullOrWhiteSpace(request.ApprovalStatus) ? null : request.ApprovalStatus;
        Guid? createdByUserId = Guid.TryParse(request.CreatedByUserId, out Guid parsedCreatedByUserId)
            ? parsedCreatedByUserId
            : null;
        (IReadOnlyList<Job> jobs, int total) = await _jobRepository.GetPagedAsync(
            normalizedDepartment,
            normalizedStatus,
            request.Page,
            request.PageSize,
            createdByUserId);
        (IReadOnlyList<Job> allMatchingJobs, _) = await _jobRepository.GetPagedAsync(
            normalizedDepartment,
            normalizedStatus,
            1,
            int.MaxValue,
            createdByUserId);

        return ApiResponse<HrJobsResponseDto>.Ok(new HrJobsResponseDto
        {
            Items = jobs.Select(job => new HrJobListItemDto
            {
                Id = job.Id.ToString(),
                Title = job.Title,
                Department = job.Department?.Name ?? string.Empty,
                CreatedDate = job.CreatedAt?.ToString("yyyy-MM-dd") ?? string.Empty,
                CreatedAt = job.CreatedAt,
                ApprovalStatus = job.Status.ToString(),
                ApplicationsCount = job.Applications.Count,
                CreatedBy = new HrJobCreatorDto
                {
                    Id = job.CreatedByNavigation.Id.ToString(),
                    FullName = job.CreatedByNavigation.FullName,
                    Email = job.CreatedByNavigation.Email
                },
                RecruiterId = job.RecruiterId?.ToString(),
                RecruiterName = job.Recruiter?.FullName,
                DepartmentHeadId = job.Department?.HeadUserId?.ToString(),
                DepartmentHeadName = job.Department?.HeadUser?.FullName,
                EffectiveDepartmentHeadId = (job.Department?.HeadUserId ?? job.ApprovedBy)?.ToString(),
                EffectiveDepartmentHeadName = (job.Department?.HeadUser ?? job.ApprovedByNavigation)?.FullName
            }).ToList(),
            Meta = PaginationMetaBuilder.Build(request.Page, request.PageSize, total),
            Stats = new HrJobStatsDto
            {
                ActiveJobs = allMatchingJobs.Count(job => job.Status == JobStatus.Approved),
                PendingApproval = allMatchingJobs.Count(job => job.Status == JobStatus.PendingApproval),
                TotalApplications = allMatchingJobs.Sum(job => job.Applications.Count),
                TimeToHireDays = 0
            }
        });
    }

    /// <summary>
    /// Retrieves manager approval queue.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ManagerJobApprovalQueueResponseDto>> GetManagerApprovalQueueAsync(ManagerJobApprovalQueryRequest request, Guid? currentUserId = null, IReadOnlyCollection<string>? currentUserRoles = null)
    {
        string? normalizedKeyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();
        string? normalizedDepartment = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim();

        // BR-OWN-003: a DepartmentHead (or legacy Manager) only sees the queue for departments they head;
        // a SystemAdmin sees every department's pending jobs. A non-head Manager therefore sees an empty
        // queue — generic Manager role no longer grants cross-department approval visibility.
        Guid? departmentHeadFilter = ResolveApprovalQueueHeadFilter(currentUserId, currentUserRoles);

        (IReadOnlyList<Job> jobs, int total) = await _jobRepository.GetPendingApprovalPagedAsync(
            normalizedKeyword,
            normalizedDepartment,
            request.Page,
            request.PageSize,
            departmentHeadFilter);

        IReadOnlyList<Job> allPendingJobs = await _jobRepository.GetPendingApprovalJobsAsync(int.MaxValue, departmentHeadFilter);
        DateTime today = DbDateTime.Now.Date;

        return ApiResponse<ManagerJobApprovalQueueResponseDto>.Ok(new ManagerJobApprovalQueueResponseDto
        {
            Items = jobs.Select(job => new ManagerJobApprovalQueueItemDto
            {
                JobId = job.Id.ToString(),
                ReferenceCode = BuildJobReferenceCode(job),
                Title = job.Title,
                DepartmentName = job.Department?.Name ?? "Unassigned",
                HiringTeamLabel = BuildHiringTeamLabel(job),
                HrOwnerName = job.CreatedByNavigation.FullName,
                Status = job.Status.ToString(),
                SubmittedAt = job.CreatedAt,
                VacancyCount = job.VacancyCount ?? 0,
                RequiredSkillsCount = job.JobSkills.Count(jobSkill => jobSkill.IsRequired),
                ApplicationsCount = job.Applications.Count,
                IsOverdue = IsApprovalOverdue(job)
            }).ToList(),
            Meta = PaginationMetaBuilder.Build(request.Page, request.PageSize, total),
            Summary = new ManagerJobApprovalSummaryDto
            {
                PendingApprovals = allPendingJobs.Count,
                SubmittedToday = allPendingJobs.Count(job => (job.CreatedAt ?? DbDateTime.Now).Date == today),
                OverdueReviews = allPendingJobs.Count(IsApprovalOverdue),
                DepartmentsWaiting = allPendingJobs
                    .Select(job => job.Department?.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count()
            }
        });
    }

    /// <summary>
    /// Retrieves manager approval detail.
    /// </summary>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<ManagerJobApprovalDetailDto>> GetManagerApprovalDetailAsync(string jobId, Guid? currentUserId = null, IReadOnlyCollection<string>? currentUserRoles = null)
    {
        Job job = await GetJobAsync(jobId);

        // BR-OWN-003: viewing the approval detail uses the SAME authorization as approving — only the
        // job's DepartmentHead or a SystemAdmin. Mirrors GuardDepartmentHeadApprovalAsync precedence
        // (422 when the department has no head, then 403 when the actor is not the head/admin).
        ApprovalAccess access = EvaluateApprovalAccess(job.Department?.HeadUserId, currentUserId, currentUserRoles);
        if (access == ApprovalAccess.NoHead)
        {
            return ApiResponse<ManagerJobApprovalDetailDto>.UnprocessableEntity(
                "This job's department has no head assigned; assign a department head before approving or rejecting.",
                errorCode: ErrorCodes.DepartmentHeadRequired);
        }

        if (access == ApprovalAccess.Forbidden)
        {
            return ApiResponse<ManagerJobApprovalDetailDto>.Forbidden(
                "Only the department head or a system administrator can view or act on this job's approval.",
                errorCode: ErrorCodes.Forbidden);
        }

        int applicationsCount = job.Applications.Count;
        int activePipelineCount = job.Applications.Count(application =>
            application.Status != ApplicationStatus.Rejected &&
            application.Status != ApplicationStatus.Hired &&
            application.Status != ApplicationStatus.OfferDeclined &&
            application.Status != ApplicationStatus.Withdrawn);

        return ApiResponse<ManagerJobApprovalDetailDto>.Ok(new ManagerJobApprovalDetailDto
        {
            JobId = job.Id.ToString(),
            ReferenceCode = BuildJobReferenceCode(job),
            Title = job.Title,
            Status = job.Status.ToString(),
            StatusLabel = MapApprovalStatusLabel(job.Status),
            SubmittedAt = job.CreatedAt,
            SubmittedAgoLabel = BuildSubmittedAgoLabel(job.CreatedAt),
            HrOwner = new ManagerJobApprovalUserDto
            {
                UserId = job.CreatedByNavigation.Id.ToString(),
                FullName = job.CreatedByNavigation.FullName,
                Email = job.CreatedByNavigation.Email,
                Phone = job.CreatedByNavigation.Phone
            },
            Department = new ManagerJobApprovalDepartmentDto
            {
                DepartmentId = job.DepartmentId?.ToString() ?? string.Empty,
                Name = job.Department?.Name ?? "Unassigned",
                Description = job.Department?.Description
            },
            Location = job.Location,
            WorkMode = job.WorkMode.ToString(),
            EmploymentType = MapEmploymentType(job.EmploymentType),
            VacancyCount = job.VacancyCount ?? 0,
            MinExperienceYears = job.MinExperienceYears,
            SalaryMin = job.SalaryMin,
            SalaryMax = job.SalaryMax,
            Deadline = job.Deadline,
            Description = ParseJsonArray(job.Description),
            Requirements = ParseJsonArray(job.Requirements),
            Benefits = ParseJsonArray(job.Benefits),
            Skills = job.JobSkills
                .OrderByDescending(jobSkill => jobSkill.IsRequired)
                .ThenBy(jobSkill => jobSkill.Skill.Name)
                .Select(jobSkill => new ManagerJobApprovalSkillDto
                {
                    SkillId = jobSkill.SkillId.ToString(),
                    Name = jobSkill.Skill.Name,
                    MinYearsExperience = jobSkill.MinYearsExperience,
                    IsRequired = jobSkill.IsRequired,
                    SkillType = jobSkill.SkillType,
                    MinimumYearsOfExperience = jobSkill.MinimumYearsOfExperience
                }).ToList(),
            Insights = new ManagerJobApprovalInsightDto
            {
                ApplicationsCount = applicationsCount,
                ActivePipelineCount = activePipelineCount,
                RequiredSkillsCount = job.JobSkills.Count(jobSkill => jobSkill.IsRequired),
                OptionalSkillsCount = job.JobSkills.Count(jobSkill => !jobSkill.IsRequired),
                HasSalaryRange = job.SalaryMin.HasValue || job.SalaryMax.HasValue
            },
            InterviewFlow =
            [
                new ManagerJobApprovalStepDto
                {
                    Order = 1,
                    Label = "Application Review",
                    Description = "HR screens incoming applications before interviews are scheduled."
                },
                new ManagerJobApprovalStepDto
                {
                    Order = 2,
                    Label = "Interview Loop",
                    Description = "Candidates move through the configured interview rounds tracked in RecruitPro."
                },
                new ManagerJobApprovalStepDto
                {
                    Order = 3,
                    Label = "Manager Review",
                    Description = "Completed interview results are escalated for manager review and final alignment."
                },
                new ManagerJobApprovalStepDto
                {
                    Order = 4,
                    Label = "Final Hiring Decision",
                    Description = "Approved roles continue toward offers and final hiring decisions."
                }
            ],
            ApprovalSnapshot = job.ApprovedByNavigation == null && job.Status == JobStatus.PendingApproval
                ? new ManagerJobApprovalHistoryDto
                {
                    Summary = "This job is still waiting for the first manager approval decision."
                }
                : new ManagerJobApprovalHistoryDto
                {
                    ApprovedByName = job.ApprovedByNavigation?.FullName,
                    LastUpdatedAt = job.CreatedAt,
                    Summary = job.ApprovedByNavigation == null
                        ? "No approval history is stored for this job yet."
                        : $"Latest approval decision was recorded under {job.ApprovedByNavigation.FullName}."
                }
        });
    }

    /// <summary>
    /// Creates job.
    /// </summary>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <param name="currentUserId">The <paramref name="currentUserId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<HrCreateJobResponseDto>> CreateJobAsync(CreateJobRequest request, Guid currentUserId)
    {
        // BR-OWN-002: a job must belong to a Department (so a DepartmentHead can own its approval/review).
        Department? department = await ResolveDepartmentAsync(request.DepartmentId, request.Department);
        if (department == null)
        {
            return ApiResponse<HrCreateJobResponseDto>.UnprocessableEntity(
                "A valid department is required to create a job.", errorCode: ErrorCodes.DepartmentNotFound);
        }

        // BR-OWN-002: RecruiterId is the business owner. When provided it must be an existing HR user;
        // when omitted we fall back to the creating user (compatibility fallback, JOB-APPROVAL-FLOW.md).
        Guid recruiterId = currentUserId;
        if (!string.IsNullOrWhiteSpace(request.RecruiterId))
        {
            if (!Guid.TryParse(request.RecruiterId, out Guid parsedRecruiterId))
            {
                return ApiResponse<HrCreateJobResponseDto>.UnprocessableEntity(
                    "The selected recruiter is not a valid user.", errorCode: ErrorCodes.InvalidJobRecruiter);
            }

            User? recruiter = await _userRepository.GetByIdAsync(parsedRecruiterId);
            if (recruiter == null || !UserHasAnyRole(recruiter, RoleNames.Hr))
            {
                return ApiResponse<HrCreateJobResponseDto>.UnprocessableEntity(
                    "The selected recruiter must be an existing user with the HR role.",
                    errorCode: ErrorCodes.InvalidJobRecruiter);
            }

            recruiterId = recruiter.Id;
        }

        List<JobSkill> jobSkills = await BuildJobSkillsAsync(request.SkillRequirements, request.SkillIds, request.Skills);
        List<string> benefits = request.Benefits.Count > 0 ? request.Benefits : request.Responsibilities;

        Job job = new()
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            ShortPitch = request.ShortPitch,
            DepartmentId = department.Id,
            CreatedBy = currentUserId,
            RecruiterId = recruiterId,
            EmploymentType = ParseEmploymentType(request.EmploymentType),
            WorkMode = ParseWorkMode(request.WorkMode),
            Location = request.Location ?? string.Empty,
            Description = request.Description ?? string.Empty,
            Requirements = SerializeList(request.Requirements),
            Benefits = SerializeList(benefits),
            SalaryMin = request.SalaryMin,
            SalaryMax = request.SalaryMax,
            MinExperienceYears = request.MinExperienceYears,
            VacancyCount = request.VacancyCount,
            Deadline = request.Deadline,
            Status = JobStatus.PendingApproval,
            CreatedAt = DbDateTime.Now,
            JobSkills = jobSkills
        };
        foreach (JobSkill jobSkill in job.JobSkills)
        {
            jobSkill.JobId = job.Id;
        }

        await _jobRepository.AddAsync(job);
        await _unitOfWork.SaveChangesAsync();
        await TryRefreshJobEmbeddingAsync(job.Id);

        // Best-effort, post-commit: notify the department head that a job awaits their approval. Reload
        // the job so its Department (HeadUserId) is available for routing. A publish failure must not
        // turn a committed job creation into a 500.
        try
        {
            Job? savedJob = await _jobRepository.GetByIdAsync(job.Id);
            if (savedJob != null)
            {
                await _notificationEventService.PublishJobSubmittedForApprovalAsync(savedJob, currentUserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Job {JobId} was created but the job-submitted-for-approval notification failed to publish.",
                job.Id);
        }

        return ApiResponse<HrCreateJobResponseDto>.Created(new HrCreateJobResponseDto
        {
            JobId = job.Id.ToString(),
            ApprovalStatus = job.Status.ToString()
        }, "Job submitted for approval");
    }

    /// <summary>
    /// Executes the patch job operation.
    /// </summary>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <param name="request">The <paramref name="request"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<HrJobStatusResponseDto>> PatchJobAsync(string jobId, PatchJobRequest request, Guid? currentUserId = null, IReadOnlyCollection<string>? currentUserRoles = null)
    {
        if (!Guid.TryParse(jobId, out Guid jobGuid))
        {
            return ApiResponse<HrJobStatusResponseDto>.NotFound("Job not found.");
        }

        Job? job = await _jobRepository.GetTrackedByIdAsync(jobGuid);
        if (job == null)
        {
            return ApiResponse<HrJobStatusResponseDto>.NotFound("Job not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            job.Title = request.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Department))
        {
            Department? department = await _jobRepository.GetDepartmentByNameAsync(request.Department);
            job.DepartmentId = department?.Id;
        }
        else if (!string.IsNullOrWhiteSpace(request.DepartmentId) && Guid.TryParse(request.DepartmentId, out Guid departmentId))
        {
            Department? department = await _jobRepository.GetDepartmentByIdAsync(departmentId);
            job.DepartmentId = department?.Id;
        }

        JobStatus? parsedStatus = ParseJobStatus(request.ApprovalStatus);
        JobStatus? approvalDecision = null;
        if (parsedStatus.HasValue)
        {
            // BR-OWN-003 / JOB-APPROVAL-FLOW.md: approval and rejection are scoped to the job's
            // DepartmentHead (Department.HeadUserId) or a SystemAdmin. Other status moves (Draft,
            // PendingApproval, Closed) keep the existing HR/Manager behavior.
            if (parsedStatus.Value is JobStatus.Approved or JobStatus.Rejected)
            {
                ApiResponse<HrJobStatusResponseDto>? guardFailure = await GuardDepartmentHeadApprovalAsync(job, currentUserId, currentUserRoles);
                if (guardFailure != null)
                {
                    return guardFailure;
                }

                // ApprovedBy records the decision actor (audit), per the current model.
                job.ApprovedBy = currentUserId;
                approvalDecision = parsedStatus.Value;
            }

            job.Status = parsedStatus.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            job.Description = request.Description;
        }

        if (request.Requirements != null)
        {
            job.Requirements = SerializeList(request.Requirements);
        }

        if (request.Benefits != null)
        {
            job.Benefits = SerializeList(request.Benefits);
        }

        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            job.Location = request.Location;
        }

        if (!string.IsNullOrWhiteSpace(request.WorkMode))
        {
            job.WorkMode = ParseWorkMode(request.WorkMode);
        }

        if (!string.IsNullOrWhiteSpace(request.EmploymentType))
        {
            job.EmploymentType = ParseEmploymentType(request.EmploymentType);
        }

        if (request.MinExperienceYears.HasValue)
        {
            job.MinExperienceYears = request.MinExperienceYears.Value;
        }

        if (request.VacancyCount.HasValue)
        {
            job.VacancyCount = request.VacancyCount.Value;
        }

        if (request.SalaryMin.HasValue)
        {
            job.SalaryMin = request.SalaryMin.Value;
        }

        if (request.SalaryMax.HasValue)
        {
            job.SalaryMax = request.SalaryMax.Value;
        }

        if (request.Deadline.HasValue)
        {
            job.Deadline = request.Deadline;
        }

        if (request.SkillRequirements != null || request.SkillIds != null || request.Skills != null)
        {
            List<JobSkill> jobSkills = await BuildJobSkillsAsync(request.SkillRequirements, request.SkillIds ?? [], request.Skills ?? []);
            job.JobSkills.Clear();
            foreach (JobSkill jobSkill in jobSkills)
            {
                jobSkill.JobId = job.Id;
                job.JobSkills.Add(jobSkill);
            }
        }

        await _jobRepository.UpdateAsync(job);
        await _unitOfWork.SaveChangesAsync();
        await TryRefreshJobEmbeddingAsync(job.Id);

        // Best-effort, post-commit: notify the recruiter of the department head's approve/reject
        // decision. A publish failure must not turn a committed decision into a 500.
        if (approvalDecision.HasValue)
        {
            try
            {
                if (approvalDecision.Value == JobStatus.Approved)
                {
                    await _notificationEventService.PublishJobApprovedAsync(job);
                }
                else
                {
                    // No rejection-reason field is currently stored on the job/patch request, so none is
                    // included (no new reason storage is invented for this phase).
                    await _notificationEventService.PublishJobRejectedAsync(job, reason: null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Job {JobId} decision ({Decision}) was saved but the job-decision notification failed to publish.",
                    job.Id,
                    approvalDecision.Value);
            }
        }

        return ApiResponse<HrJobStatusResponseDto>.Ok(new HrJobStatusResponseDto
        {
            JobId = job.Id.ToString(),
            ApprovalStatus = job.Status.ToString()
        });
    }

    /// <summary>
    /// Deletes job.
    /// </summary>
    /// <param name="jobId">The <paramref name="jobId"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<ApiResponse<string>> DeleteJobAsync(string jobId)
    {
        if (!Guid.TryParse(jobId, out Guid jobGuid))
        {
            return ApiResponse<string>.NotFound("Job not found.");
        }

        Job? job = await _jobRepository.GetTrackedByIdAsync(jobGuid);
        if (job == null)
        {
            return ApiResponse<string>.NotFound("Job not found.");
        }

        await _jobRepository.DeleteAsync(job);
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<string>.Ok("Job deleted successfully", "Job deleted successfully");
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


    private async Task TryRefreshJobEmbeddingAsync(Guid jobId)
    {
        try
        {
            await _semanticDiscoveryService.RefreshJobEmbeddingAsync(jobId);
        }
        catch
        {
        }
    }

    private static JobListItemDto MapJobListItem(Job job)
    {
        return new JobListItemDto
        {
            Id = job.Id.ToString(),
            Title = job.Title,
            ShortPitch = job.ShortPitch,
            Department = job.Department?.Name ?? string.Empty,
            Location = job.Location,
            WorkMode = job.WorkMode.ToString(),
            SalaryMin = job.SalaryMin,
            SalaryMax = job.SalaryMax,
            PostedAt = job.CreatedAt,
            Tags = job.JobSkills.Select(jobSkill => jobSkill.Skill.Name).Distinct().ToList(),
            ShortDescription = ParseJsonArray(job.Description).FirstOrDefault() ?? string.Empty,
            EmploymentType = MapEmploymentType(job.EmploymentType)
        };
    }

    private static JobDetailResponseDto MapLegacyJobDetail(Job job)
    {
        // Effective department head = the department's head, falling back to the approver (audit). See
        // BR-OWN-003 / RECRUITMENT-OWNERSHIP-MATRIX.md.
        Guid? effectiveHeadId = job.Department?.HeadUserId ?? job.ApprovedBy;
        User? effectiveHeadUser = job.Department?.HeadUser ?? job.ApprovedByNavigation;

        return new JobDetailResponseDto
        {
            Id = job.Id,
            Title = job.Title,
            Department = job.Department?.Name ?? string.Empty,
            Location = job.Location,
            WorkMode = job.WorkMode.ToString(),
            Requirements = ParseJsonArray(job.Requirements),
            RequiredSkills = job.JobSkills.Where(jobSkill => jobSkill.IsRequired).Select(MapJobSkill).ToList(),
            NiceToHaveSkills = job.JobSkills.Where(jobSkill => !jobSkill.IsRequired).Select(MapJobSkill).ToList(),
            Skills = job.JobSkills.Select(MapJobSkill).ToList(),
            SalaryMin = job.SalaryMin,
            SalaryMax = job.SalaryMax,
            Deadline = job.Deadline,
            JobType = $"{MapEmploymentType(job.EmploymentType)} / {job.WorkMode}",
            SalaryRange = CompensationLabelHelper.BuildSalaryLabel(job.SalaryMin, job.SalaryMax),
            SalaryLabel = CompensationLabelHelper.BuildSalaryLabel(job.SalaryMin, job.SalaryMax),
            Posted = job.CreatedAt?.ToString("yyyy-MM-dd") ?? string.Empty,
            VacancyCount = job.VacancyCount,
            Status = job.Status.ToString(),
            Description = ParseJsonArray(job.Description),

            RecruiterId = job.RecruiterId?.ToString(),
            RecruiterName = job.Recruiter?.FullName,
            RecruiterEmail = job.Recruiter?.Email,

            DepartmentHeadId = job.Department?.HeadUserId?.ToString(),
            DepartmentHeadName = job.Department?.HeadUser?.FullName,
            DepartmentHeadEmail = job.Department?.HeadUser?.Email,

            EffectiveDepartmentHeadId = effectiveHeadId?.ToString(),
            EffectiveDepartmentHeadName = effectiveHeadUser?.FullName,
            EffectiveDepartmentHeadEmail = effectiveHeadUser?.Email,

            CreatedBy = job.CreatedBy.ToString(),
            CreatedByName = job.CreatedByNavigation?.FullName,
            ApprovedBy = job.ApprovedBy?.ToString(),
            ApprovedByName = job.ApprovedByNavigation?.FullName
        };
    }

    private static string BuildJobReferenceCode(Job job)
    {
        return $"JOB-{job.CreatedAt?.Year ?? DbDateTime.Now.Year}-{job.Id.ToString()[..8].ToUpperInvariant()}";
    }

    private static string BuildHiringTeamLabel(Job job)
    {
        string departmentName = job.Department?.Name ?? "General";
        return string.IsNullOrWhiteSpace(job.Location)
            ? departmentName
            : $"{departmentName} / {job.Location}";
    }

    private static string MapApprovalStatusLabel(JobStatus status)
    {
        return status switch
        {
            JobStatus.PendingApproval => "Pending Approval",
            JobStatus.Approved => "Approved",
            JobStatus.Rejected => "Rejected",
            JobStatus.Draft => "Draft",
            JobStatus.Closed => "Closed",
            _ => status.ToString()
        };
    }

    private static string BuildSubmittedAgoLabel(DateTime? createdAt)
    {
        if (!createdAt.HasValue)
        {
            return "Submission time unavailable";
        }

        TimeSpan age = DbDateTime.Now - createdAt.Value;
        if (age.TotalHours < 1)
        {
            int minutes = Math.Max(1, (int)Math.Floor(age.TotalMinutes));
            return $"Submitted {minutes} minute{(minutes == 1 ? string.Empty : "s")} ago";
        }

        if (age.TotalDays < 1)
        {
            int hours = Math.Max(1, (int)Math.Floor(age.TotalHours));
            return $"Submitted {hours} hour{(hours == 1 ? string.Empty : "s")} ago";
        }

        int days = Math.Max(1, (int)Math.Floor(age.TotalDays));
        return $"Submitted {days} day{(days == 1 ? string.Empty : "s")} ago";
    }

    /// <summary>
    /// Builds meta.
    /// </summary>
    /// <param name="page">The <paramref name="page"/> value.</param>
    /// <param name="pageSize">The <paramref name="pageSize"/> value.</param>
    /// <param name="total">The <paramref name="total"/> value.</param>
    /// <returns>The operation result.</returns>
    /// <summary>
    /// Serializes list.
    /// </summary>
    /// <param name="values">The <paramref name="values"/> value.</param>
    /// <returns>The operation result.</returns>
    private static string? SerializeList(List<string> values)
    {
        return values.Count == 0 ? null : JsonSerializer.Serialize(values);
    }

    /// <summary>
    /// Builds job reference code.
    /// </summary>
    /// <param name="job">The <paramref name="job"/> value.</param>
    /// <returns>The resulting string value.</returns>
    /// <summary>
    /// Resolves department.
    /// </summary>
    /// <param name="departmentId">The <paramref name="departmentId"/> value.</param>
    /// <param name="departmentName">The <paramref name="departmentName"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <summary>
    /// Authorizes a job approve/reject against the job's DepartmentHead (Department.HeadUserId) or a
    /// SystemAdmin. Returns a non-null failure response to short-circuit; null when allowed.
    /// </summary>
    private async Task<ApiResponse<HrJobStatusResponseDto>?> GuardDepartmentHeadApprovalAsync(
        Job job,
        Guid? currentUserId,
        IReadOnlyCollection<string>? currentUserRoles)
    {
        Guid? headUserId = null;
        if (job.DepartmentId.HasValue)
        {
            // The tracked job includes its (original) Department; reload if the patch just changed it.
            headUserId = job.Department != null && job.Department.Id == job.DepartmentId.Value
                ? job.Department.HeadUserId
                : (await _jobRepository.GetDepartmentByIdAsync(job.DepartmentId.Value))?.HeadUserId;
        }

        ApprovalAccess access = EvaluateApprovalAccess(headUserId, currentUserId, currentUserRoles);
        if (access == ApprovalAccess.NoHead)
        {
            return ApiResponse<HrJobStatusResponseDto>.UnprocessableEntity(
                "This job's department has no head assigned; assign a department head before approving or rejecting.",
                errorCode: ErrorCodes.DepartmentHeadRequired);
        }

        if (access == ApprovalAccess.Forbidden)
        {
            return ApiResponse<HrJobStatusResponseDto>.Forbidden(
                "Only the department head or a system administrator can approve or reject this job.",
                errorCode: ErrorCodes.Forbidden);
        }

        return null;
    }

    /// <summary>
    /// The outcome of evaluating whether a user may act on a job's approval. Shared by the approval
    /// submit guard, the approval detail guard, and (indirectly) the queue scope so all three honor the
    /// same BR-OWN-003 rule: only the job's DepartmentHead or a SystemAdmin, and a department with no
    /// head cannot be approved/reviewed at all.
    /// </summary>
    private enum ApprovalAccess
    {
        Allowed,
        NoHead,
        Forbidden
    }

    private static ApprovalAccess EvaluateApprovalAccess(
        Guid? headUserId,
        Guid? currentUserId,
        IReadOnlyCollection<string>? currentUserRoles)
    {
        if (headUserId == null)
        {
            return ApprovalAccess.NoHead;
        }

        bool isHead = currentUserId.HasValue && currentUserId.Value == headUserId.Value;
        bool isSystemAdmin = currentUserRoles != null && currentUserRoles.Contains(RoleNames.SystemAdmin);
        return isHead || isSystemAdmin ? ApprovalAccess.Allowed : ApprovalAccess.Forbidden;
    }

    /// <summary>
    /// Resolves the department-head filter for the approval queue. A SystemAdmin sees every department's
    /// pending jobs (null filter); everyone else (DepartmentHead, legacy Manager) is scoped to the
    /// departments they head. A non-head therefore gets an empty queue — generic Manager role no longer
    /// grants cross-department approval visibility (BR-OWN-003).
    /// </summary>
    private static Guid? ResolveApprovalQueueHeadFilter(
        Guid? currentUserId,
        IReadOnlyCollection<string>? currentUserRoles)
    {
        bool isSystemAdmin = currentUserRoles != null && currentUserRoles.Contains(RoleNames.SystemAdmin);
        if (isSystemAdmin)
        {
            return null;
        }

        // Non-admin with no identity must never fall through to the unscoped (all-departments) query;
        // Guid.Empty never matches a real Department.HeadUserId, so the queue comes back empty.
        return currentUserId ?? Guid.Empty;
    }

    private static bool UserHasAnyRole(User user, params string[] roleNames)
    {
        return user.UserRoles.Any(userRole => roleNames.Contains(userRole.Role.Name));
    }

    private static DepartmentResponseDto MapDepartment(Department department)
    {
        return new DepartmentResponseDto
        {
            Id = department.Id.ToString(),
            Name = department.Name,
            Description = department.Description,
            HeadUserId = department.HeadUserId?.ToString(),
            HeadUserName = department.HeadUser?.FullName,
            HeadUserEmail = department.HeadUser?.Email
        };
    }

    private async Task<Department?> ResolveDepartmentAsync(string? departmentId, string? departmentName)
    {
        if (!string.IsNullOrWhiteSpace(departmentId) && Guid.TryParse(departmentId, out Guid parsedDepartmentId))
        {
            return await _jobRepository.GetDepartmentByIdAsync(parsedDepartmentId);
        }

        if (!string.IsNullOrWhiteSpace(departmentName))
        {
            return await _jobRepository.GetDepartmentByNameAsync(departmentName);
        }

        return null;
    }

    /// <summary>
    /// Builds job skills.
    /// </summary>
    /// <param name="skillRequirements">The <paramref name="skillRequirements"/> value.</param>
    /// <param name="skillIds">The <paramref name="skillIds"/> value.</param>
    /// <param name="skillNames">The <paramref name="skillNames"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    private async Task<List<JobSkill>> BuildJobSkillsAsync(
        IReadOnlyCollection<JobSkillRequirementRequest>? skillRequirements,
        IReadOnlyCollection<string> skillIds,
        IReadOnlyCollection<string> skillNames)
    {
        if (skillRequirements != null && skillRequirements.Count > 0)
        {
            List<Skill> allSkills = (await _jobRepository.GetSkillsAsync()).ToList();

            return skillRequirements
                .Select(requirement =>
                {
                    Skill? matchedSkill = !string.IsNullOrWhiteSpace(requirement.SkillId) && Guid.TryParse(requirement.SkillId, out Guid requirementSkillId)
                        ? allSkills.FirstOrDefault(skill => skill.Id == requirementSkillId)
                        : allSkills.FirstOrDefault(skill =>
                            !string.IsNullOrWhiteSpace(requirement.SkillName)
                            && skill.Name.Equals(requirement.SkillName, StringComparison.OrdinalIgnoreCase));

                    return matchedSkill == null
                        ? null
                        : new JobSkill
                        {
                            JobId = Guid.Empty,
                            SkillId = matchedSkill.Id,
                            IsRequired = !string.Equals(requirement.SkillType, "NiceToHave", StringComparison.OrdinalIgnoreCase),
                            MinYearsExperience = requirement.MinimumYearsOfExperience
                        };
                })
                .Where(jobSkill => jobSkill != null)
                .GroupBy(jobSkill => jobSkill!.SkillId)
                .Select(group => group
                    .OrderByDescending(jobSkill => jobSkill!.IsRequired)
                    .ThenByDescending(jobSkill => jobSkill!.MinYearsExperience ?? 0)
                    .First()!)
                .ToList();
        }

        List<Guid> parsedSkillIds = skillIds
            .Select(value => Guid.TryParse(value, out Guid parsed) ? parsed : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToList();

        IReadOnlyList<Skill> skills = parsedSkillIds.Count > 0
            ? await _skillRepository.GetByIdsAsync(parsedSkillIds)
            : (await _jobRepository.GetSkillsAsync())
                .Where(skill => skillNames.Any(name => name.Equals(skill.Name, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        return skills
            .DistinctBy(skill => skill.Id)
            .Select(skill => new JobSkill
            {
                JobId = Guid.Empty,
                SkillId = skill.Id,
                IsRequired = true
            })
            .ToList();
    }

    private static bool IsApprovalOverdue(Job job)
    {
        return job.CreatedAt.HasValue && job.CreatedAt.Value <= DbDateTime.Now.AddDays(-3);
    }

    private static List<string> ParseJsonArray(string? jsonString)
    {
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return [];
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(jsonString);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return doc.RootElement.EnumerateArray()
                    .Select(element => element.GetString() ?? string.Empty)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList();
            }
        }
        catch
        {
        }

        return [jsonString];
    }

    private static JobSkillDto MapJobSkill(JobSkill jobSkill)
    {
        return new JobSkillDto
        {
            Id = jobSkill.SkillId,
            Name = jobSkill.Skill.Name,
            MinYearsExperience = jobSkill.MinYearsExperience,
            IsRequired = jobSkill.IsRequired,
            SkillType = jobSkill.SkillType,
            MinimumYearsOfExperience = jobSkill.MinimumYearsOfExperience
        };
    }

    private static string MapEmploymentType(EmploymentType type)
    {
        return type switch
        {
            EmploymentType.FullTime => "Full-time",
            EmploymentType.PartTime => "Part-time",
            EmploymentType.Internship => "Internship",
            EmploymentType.Contract => "Contract",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// Maps job skill.
    /// </summary>
    /// <param name="jobSkill">The <paramref name="jobSkill"/> value.</param>
    /// <returns>The operation result.</returns>
    /// <summary>
    /// Parses employment type.
    /// </summary>
    /// <param name="value">The <paramref name="value"/> value.</param>
    /// <returns>The operation result.</returns>
    private static EmploymentType ParseEmploymentType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "part-time" or "parttime" => EmploymentType.PartTime,
            "internship" => EmploymentType.Internship,
            "contract" => EmploymentType.Contract,
            _ => EmploymentType.FullTime
        };
    }

    /// <summary>
    /// Parses work mode.
    /// </summary>
    /// <param name="value">The <paramref name="value"/> value.</param>
    /// <returns>The operation result.</returns>
    private static WorkMode ParseWorkMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "onsite" => WorkMode.Onsite,
            "hybrid" => WorkMode.Hybrid,
            _ => WorkMode.Remote
        };
    }

    /// <summary>
    /// Parses job status.
    /// </summary>
    /// <param name="value">The <paramref name="value"/> value.</param>
    /// <returns>The operation result.</returns>
    private static JobStatus? ParseJobStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "draft" => JobStatus.Draft,
            "pending" or "pendingapproval" => JobStatus.PendingApproval,
            "approved" => JobStatus.Approved,
            "closed" => JobStatus.Closed,
            "rejected" => JobStatus.Rejected,
            _ => null
        };
    }

    /// <summary>
    /// Maps employment type.
    /// </summary>
    /// <param name="type">The <paramref name="type"/> value.</param>
    /// <returns>The resulting string value.</returns>
}
