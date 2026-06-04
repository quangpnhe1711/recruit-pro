using System.Text.Json;
using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Services;

public class HrService : IHrService
{
    private readonly IHrRepository _hrRepository;
    private readonly IUnitOfWork _unitOfWork;

    public HrService(IHrRepository hrRepository, IUnitOfWork unitOfWork)
    {
        _hrRepository = hrRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<HrDashboardDto>> GetDashboardAsync()
    {
        var today = DateTime.UtcNow.Date;
        var nextInterview = await _hrRepository.GetNextInterviewAsync(today);
        var recentApplications = await _hrRepository.GetRecentApplicationsAsync(5);
        var pendingApprovals = await _hrRepository.GetPendingApprovalJobsAsync(5);

        return ApiResponse<HrDashboardDto>.Ok(new HrDashboardDto
        {
            Stats = new HrDashboardStatsDto
            {
                ActivePostings = await _hrRepository.CountApprovedJobsAsync(),
                TotalApplicants = await _hrRepository.CountApplicationsAsync(),
                InterviewsToday = await _hrRepository.CountInterviewsOnDateAsync(today),
                NextInterviewLabel = nextInterview == null ? string.Empty : $"{nextInterview.InterviewDate:HH:mm} ({nextInterview.Id.ToString()[..6]})"
            },
            RecentApplications = recentApplications.Select(x => new HrRecentApplicationDto
            {
                ApplicationId = x.Id.ToString(),
                CandidateName = x.User.FullName,
                JobAppliedFor = x.Job.Title,
                Status = x.Status.ToString(),
                Date = (x.AppliedAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd")
            }).ToList(),
            PendingApprovals = pendingApprovals.Select(x => new HrPendingApprovalDto
            {
                JobId = x.Id.ToString(),
                Title = x.Title,
                Meta = $"{x.Department?.Name ?? "General"} • {x.WorkMode}",
                ApproverCount = 1
            }).ToList()
        });
    }

    public async Task<ApiResponse<HrJobsResponseDto>> GetJobsAsync(HrJobQueryRequest request)
    {
        var parsedStatus = ParseJobStatus(request.ApprovalStatus);
        var (jobs, total) = await _hrRepository.GetJobsAsync(request.Department, parsedStatus, request.Page, request.PageSize);

        return ApiResponse<HrJobsResponseDto>.Ok(new HrJobsResponseDto
        {
            Items = jobs.Select(x => new HrJobListItemDto
            {
                Id = x.Id.ToString(),
                Title = x.Title,
                Department = x.Department?.Name ?? string.Empty,
                CreatedDate = x.CreatedAt?.ToString("yyyy-MM-dd") ?? string.Empty,
                ApprovalStatus = x.Status.ToString(),
                ApplicationsCount = x.Applications.Count
            }).ToList(),
            Meta = BuildMeta(request.Page, request.PageSize, total),
            Stats = new HrJobStatsDto
            {
                ActiveJobs = await _hrRepository.CountApprovedJobsAsync(),
                PendingApproval = (await _hrRepository.GetPendingApprovalJobsAsync(int.MaxValue)).Count,
                TotalApplications = await _hrRepository.CountApplicationsAsync(),
                TimeToHireDays = 0
            }
        });
    }

    public async Task<ApiResponse<HrCreateJobResponseDto>> CreateJobAsync(CreateJobRequest request, Guid currentUserId)
    {
        var department = string.IsNullOrWhiteSpace(request.Department)
            ? null
            : await _hrRepository.GetDepartmentByNameAsync(request.Department);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            ShortPitch = request.ShortPitch,
            DepartmentId = department?.Id,
            CreatedBy = currentUserId,
            EmploymentType = ParseEmploymentType(request.EmploymentType),
            WorkMode = ParseWorkMode(request.WorkMode),
            Location = request.Location ?? string.Empty,
            Description = request.Description ?? string.Empty,
            Requirements = SerializeList(request.Requirements),
            Benefits = SerializeList(request.Responsibilities),
            SalaryMin = request.SalaryMin,
            SalaryMax = request.SalaryMax,
            VacancyCount = request.VacancyCount,
            Status = JobStatus.PendingApproval,
            CreatedAt = DateTime.UtcNow
        };

        await _hrRepository.AddJobAsync(job);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<HrCreateJobResponseDto>.Created(new HrCreateJobResponseDto
        {
            JobId = job.Id.ToString(),
            ApprovalStatus = job.Status.ToString()
        }, "Job submitted for approval");
    }

    public async Task<ApiResponse<HrJobStatusResponseDto>> PatchJobAsync(string jobId, PatchJobRequest request)
    {
        if (!Guid.TryParse(jobId, out var jobGuid))
        {
            return ApiResponse<HrJobStatusResponseDto>.NotFound("Job not found.");
        }

        var job = await _hrRepository.GetJobByIdAsync(jobGuid);
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
            var department = await _hrRepository.GetDepartmentByNameAsync(request.Department);
            job.DepartmentId = department?.Id;
        }

        var parsedStatus = ParseJobStatus(request.ApprovalStatus);
        if (parsedStatus.HasValue)
        {
            job.Status = parsedStatus.Value;
        }

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<HrJobStatusResponseDto>.Ok(new HrJobStatusResponseDto
        {
            JobId = job.Id.ToString(),
            ApprovalStatus = job.Status.ToString()
        });
    }

    public async Task<ApiResponse<string>> DeleteJobAsync(string jobId)
    {
        if (!Guid.TryParse(jobId, out var jobGuid))
        {
            return ApiResponse<string>.NotFound("Job not found.");
        }

        var job = await _hrRepository.GetJobByIdAsync(jobGuid);
        if (job == null)
        {
            return ApiResponse<string>.NotFound("Job not found.");
        }

        await _hrRepository.DeleteJobAsync(job);
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<string>.Ok("Job deleted successfully", "Job deleted successfully");
    }

    public async Task<ApiResponse<HrCandidatesResponseDto>> GetCandidatesAsync(int page, int pageSize, string? keyword, string? status, string? source)
    {
        var (candidates, total) = await _hrRepository.GetCandidatesAsync(page, pageSize, keyword);

        var items = candidates.Select(x =>
        {
            var names = x.User.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var latestApplication = x.User.Applications.OrderByDescending(a => a.AppliedAt).FirstOrDefault();
            return new HrCandidateListItemDto
            {
                Id = x.Id.ToString(),
                FirstName = names.FirstOrDefault() ?? x.User.FullName,
                LastName = names.Length > 1 ? string.Join(' ', names.Skip(1)) : string.Empty,
                FullName = x.User.FullName,
                Email = x.User.Email,
                AvatarUrl = x.User.AvatarUrl,
                Source = source ?? "Portal",
                AppliedDate = latestApplication?.AppliedAt?.ToString("yyyy-MM-dd") ?? string.Empty,
                Status = latestApplication?.Status.ToString() ?? "New"
            };
        });

        if (!string.IsNullOrWhiteSpace(status))
        {
            items = items.Where(x => x.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        return ApiResponse<HrCandidatesResponseDto>.Ok(new HrCandidatesResponseDto
        {
            Items = items.ToList(),
            Meta = BuildMeta(page, pageSize, total)
        });
    }

    public async Task<ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>> GetApplicationsAsync(int page, int pageSize, string? keyword, string? department, string? status)
    {
        var parsedStatus = ParseApplicationStatus(status);
        var (applications, total) = await _hrRepository.GetApplicationsAsync(page, pageSize, keyword, department, parsedStatus);

        return ApiResponse<PaginatedResponseDto<ApplicationListItemDto>>.Ok(new PaginatedResponseDto<ApplicationListItemDto>
        {
            Items = applications.Select(x => new ApplicationListItemDto
            {
                Id = x.Id.ToString(),
                Candidate = new ApplicationCandidateSummaryDto
                {
                    Id = x.UserId.ToString(),
                    FullName = x.User.FullName,
                    Email = x.User.Email,
                    AvatarUrl = x.User.AvatarUrl,
                    CurrentPosition = x.User.CandidateProfile?.CurrentPosition
                },
                Job = new ApplicationJobSummaryDto
                {
                    Id = x.Job.Id.ToString(),
                    Title = x.Job.Title,
                    Department = new DepartmentDto
                    {
                        Id = x.Job.Department?.Id.ToString() ?? string.Empty,
                        Name = x.Job.Department?.Name ?? string.Empty
                    }
                },
                AppliedAt = x.AppliedAt ?? DateTime.UtcNow,
                Status = x.Status.ToString(),
                ReviewedBy = x.ReviewedByNavigation == null
                    ? null
                    : new UserDto
                    {
                        Id = x.ReviewedByNavigation.Id,
                        FullName = x.ReviewedByNavigation.FullName,
                        Email = x.ReviewedByNavigation.Email
                    }
            }).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = total
        });
    }

    public async Task<ApiResponse<ResumeFileResponseDto>> GetApplicationCvAsync(string applicationId)
    {
        if (!Guid.TryParse(applicationId, out var applicationGuid))
        {
            return ApiResponse<ResumeFileResponseDto>.NotFound("Application not found.");
        }

        var application = await _hrRepository.GetApplicationByIdAsync(applicationGuid);
        if (application == null)
        {
            return ApiResponse<ResumeFileResponseDto>.NotFound("Application not found.");
        }

        return ApiResponse<ResumeFileResponseDto>.Ok(new ResumeFileResponseDto
        {
            ResumeId = application.UserId.ToString(),
            FileName = Path.GetFileName(application.User.CandidateProfile?.ResumeUrl ?? string.Empty),
            FileUrl = application.User.CandidateProfile?.ResumeUrl ?? string.Empty
        });
    }

    public async Task<ApiResponse<InterviewListResponseDto>> GetInterviewsAsync(int page, int pageSize, string? keyword, string? status, DateTime? startDate, DateTime? endDate)
    {
        var parsedStatus = ParseInterviewStatus(status);
        var (interviews, total) = await _hrRepository.GetInterviewsAsync(page, pageSize, keyword, parsedStatus, startDate, endDate);

        return ApiResponse<InterviewListResponseDto>.Ok(new InterviewListResponseDto
        {
            Items = interviews.Select(x => new InterviewListItemDto
            {
                Id = x.Id.ToString(),
                CandidateName = x.Application.User.FullName,
                CandidateEmail = x.Application.User.Email,
                JobTitle = x.Application.Job.Title,
                Interviewer = "RecruitPro HR",
                DateLabel = x.InterviewDate.ToString("MMM dd, yyyy"),
                TimeLabel = $"{x.InterviewDate:HH:mm} - {x.InterviewDate.AddHours(1):HH:mm}",
                StartAt = x.InterviewDate,
                EndAt = x.InterviewDate.AddHours(1),
                Status = (x.Status ?? InterviewStatus.Scheduled).ToString()
            }).ToList(),
            Meta = BuildMeta(page, pageSize, total)
        });
    }

    public async Task<ApiResponse<ScheduleDataResponseDto>> GetScheduleDataAsync()
    {
        var candidate = await _hrRepository.GetFirstCandidateAsync();
        var interviewers = await _hrRepository.GetInterviewersAsync(5);

        return ApiResponse<ScheduleDataResponseDto>.Ok(new ScheduleDataResponseDto
        {
            Candidate = candidate == null
                ? new ScheduleCandidateDto()
                : new ScheduleCandidateDto
                {
                    Id = candidate.Id.ToString(),
                    Name = candidate.User.FullName,
                    RoleLabel = candidate.CurrentPosition ?? string.Empty,
                    AppliedFor = candidate.User.Applications.FirstOrDefault()?.Job?.Title ?? string.Empty,
                    AvatarUrl = candidate.User.AvatarUrl
                },
            Interviewers = interviewers.Select(x => new ScheduleInterviewerDto
            {
                Id = x.Id.ToString(),
                Name = x.FullName,
                Title = x.UserRoles.Select(r => r.Role.Name).FirstOrDefault() ?? "HR",
                AvatarUrl = x.AvatarUrl
            }).ToList(),
            SlotMinutes = [540, 600, 660, 690, 780, 870, 960, 1020]
        });
    }

    public async Task<ApiResponse<InterviewCreatedResponseDto>> CreateInterviewAsync(CreateInterviewRequest request)
    {
        if (!Guid.TryParse(request.ApplicationId, out var applicationGuid))
        {
            return ApiResponse<InterviewCreatedResponseDto>.BadRequest("Invalid application id.");
        }

        var interview = new Interview
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationGuid,
            InterviewDate = request.Date.ToDateTime(TimeOnly.MinValue).AddMinutes(request.StartMinutes),
            MeetingType = request.Mode.Equals("video", StringComparison.OrdinalIgnoreCase) ? MeetingType.Online : MeetingType.Offline,
            MeetingLink = request.Mode.Equals("video", StringComparison.OrdinalIgnoreCase) ? request.LocationOrLink : null,
            Location = request.Mode.Equals("video", StringComparison.OrdinalIgnoreCase) ? null : request.LocationOrLink,
            Notes = null,
            Status = ParseInterviewStatus(request.Status) ?? InterviewStatus.Scheduled
        };

        await _hrRepository.AddInterviewAsync(interview);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<InterviewCreatedResponseDto>.Created(new InterviewCreatedResponseDto
        {
            InterviewId = interview.Id.ToString()
        }, "Interview scheduled successfully");
    }

    public async Task<ApiResponse<string>> UpdateInterviewStatusAsync(string interviewId, UpdateInterviewStatusRequest request)
    {
        if (!Guid.TryParse(interviewId, out var interviewGuid))
        {
            return ApiResponse<string>.NotFound("Interview not found.");
        }

        var interview = await _hrRepository.GetInterviewByIdAsync(interviewGuid);
        if (interview == null)
        {
            return ApiResponse<string>.NotFound("Interview not found.");
        }

        var parsedStatus = ParseInterviewStatus(request.Status);
        if (!parsedStatus.HasValue)
        {
            return ApiResponse<string>.BadRequest("Invalid interview status.");
        }

        interview.Status = parsedStatus.Value;
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<string>.Ok("Interview status updated successfully");
    }

    public async Task<ApiResponse<string>> DeleteInterviewAsync(string interviewId)
    {
        if (!Guid.TryParse(interviewId, out var interviewGuid))
        {
            return ApiResponse<string>.NotFound("Interview not found.");
        }

        var interview = await _hrRepository.GetInterviewByIdAsync(interviewGuid);
        if (interview == null)
        {
            return ApiResponse<string>.NotFound("Interview not found.");
        }

        await _hrRepository.DeleteInterviewAsync(interview);
        await _unitOfWork.SaveChangesAsync();
        return ApiResponse<string>.Ok("Interview cancelled successfully", "Interview cancelled successfully");
    }

    private static ApiEnvelopeMeta BuildMeta(int page, int pageSize, int totalItems)
    {
        return new ApiEnvelopeMeta
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    private static string? SerializeList(List<string> values)
    {
        return values.Count == 0 ? null : JsonSerializer.Serialize(values);
    }

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

    private static WorkMode ParseWorkMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "onsite" => WorkMode.Onsite,
            "hybrid" => WorkMode.Hybrid,
            _ => WorkMode.Remote
        };
    }

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

    private static ApplicationStatus? ParseApplicationStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "pending" => ApplicationStatus.Pending,
            "reviewing" => ApplicationStatus.Reviewing,
            "interviewing" => ApplicationStatus.Interviewing,
            "managerreview" or "manager-review" => ApplicationStatus.ManagerReview,
            "accepted" => ApplicationStatus.Accepted,
            "rejected" => ApplicationStatus.Rejected,
            _ => null
        };
    }

    private static InterviewStatus? ParseInterviewStatus(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "scheduled" => InterviewStatus.Scheduled,
            "completed" => InterviewStatus.Completed,
            "canceled" or "cancelled" => InterviewStatus.Canceled,
            _ => null
        };
    }
}
