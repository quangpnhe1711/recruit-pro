using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Common;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly ICandidateProfileRepository _candidateProfileRepository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IJobRepository _jobRepository;
    private readonly IInterviewRepository _interviewRepository;
    private readonly INotificationRepository _notificationRepository;

    public DashboardService(
        ICandidateProfileRepository candidateProfileRepository,
        IApplicationRepository applicationRepository,
        IJobRepository jobRepository,
        IInterviewRepository interviewRepository,
        INotificationRepository notificationRepository)
    {
        _candidateProfileRepository = candidateProfileRepository;
        _applicationRepository = applicationRepository;
        _jobRepository = jobRepository;
        _interviewRepository = interviewRepository;
        _notificationRepository = notificationRepository;
    }

    public async Task<ApiResponse<CandidateDashboardDto>> GetCandidateDashboardAsync(Guid userId)
    {
        CandidateProfile profile = await GetProfileEntityAsync(userId);
        IReadOnlyList<Domain.Entities.Application> allCandidateApplications = await _applicationRepository.GetByUserIdAsync(profile.UserId);
        IReadOnlyList<Job> recommended = (await _jobRepository.SearchApprovedAsync(profile.CurrentPosition, [], profile.Skills.Select(skill => skill.Name).ToList(), "newest", 1, 5)).Jobs;

        var upcomingInterview = allCandidateApplications
            .SelectMany(application => application.Interviews.Select(interview => new { application, interview }))
            .Where(item => item.interview.InterviewDate >= DbDateTime.Now)
            .OrderBy(item => item.interview.InterviewDate)
            .FirstOrDefault();

        return ApiResponse<CandidateDashboardDto>.Ok(new CandidateDashboardDto
        {
            GreetingName = profile.User.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? profile.User.FullName,
            Stats = new CandidateDashboardStatsDto
            {
                AppliedJobs = allCandidateApplications.Count,
                Interviews = allCandidateApplications.SelectMany(application => application.Interviews).Count(),
                UnreadNotifications = await _notificationRepository.CountUnreadByUserIdAsync(profile.UserId)
            },
            UpcomingInterview = upcomingInterview == null ? null : new UpcomingInterviewDto
            {
                Id = upcomingInterview.interview.Id.ToString(),
                Date = DateOnly.FromDateTime(upcomingInterview.interview.InterviewDate).ToString("yyyy-MM-dd"),
                Time = upcomingInterview.interview.InterviewDate.ToString("HH:mm"),
                JobTitle = upcomingInterview.application.Job.Title,
                InterviewerName = "RecruitPro Interviewer",
                InterviewerTitle = "HR",
                MeetingUrl = upcomingInterview.interview.MeetingLink
            },
            RecommendedJobs = recommended.Select(job => new RecommendedJobDto
            {
                Id = job.Id.ToString(),
                Title = job.Title,
                Meta = $"{job.WorkMode} • {(job.SalaryMin.HasValue || job.SalaryMax.HasValue ? $"{job.SalaryMin:0}-{job.SalaryMax:0}" : "Negotiable")}",
                EmploymentType = job.EmploymentType.ToString(),
                Skills = job.JobSkills.Select(jobSkill => jobSkill.Skill.Name).Distinct().ToList()
            }).ToList()
        });
    }

    public async Task<ApiResponse<HrDashboardDto>> GetHrDashboardAsync()
    {
        DateTime today = DbDateTime.Today;
        Interview? nextInterview = await _interviewRepository.GetNextAsync(today);
        IReadOnlyList<Domain.Entities.Application> recentApplications = await _applicationRepository.GetRecentAsync(5);
        IReadOnlyList<Job> pendingApprovals = await _jobRepository.GetPendingApprovalJobsAsync(5);

        return ApiResponse<HrDashboardDto>.Ok(new HrDashboardDto
        {
            Stats = new HrDashboardStatsDto
            {
                ActivePostings = await _jobRepository.CountApprovedJobsAsync(),
                TotalApplicants = await _applicationRepository.CountAsync(),
                InterviewsToday = await _interviewRepository.CountOnDateAsync(today),
                NextInterviewLabel = nextInterview == null ? string.Empty : $"{nextInterview.InterviewDate:HH:mm} ({nextInterview.Id.ToString()[..6]})"
            },
            RecentApplications = recentApplications.Select(application => new HrRecentApplicationDto
            {
                ApplicationId = application.Id.ToString(),
                CandidateName = application.User.FullName,
                JobAppliedFor = application.Job.Title,
                Status = application.Status.ToString(),
                Date = (application.AppliedAt ?? DbDateTime.Now).ToString("yyyy-MM-dd")
            }).ToList(),
            PendingApprovals = pendingApprovals.Select(job => new HrPendingApprovalDto
            {
                JobId = job.Id.ToString(),
                Title = job.Title,
                Meta = $"{job.Department?.Name ?? "General"} • {job.WorkMode}",
                ApproverCount = 1
            }).ToList()
            ,
            HiringVelocity = new HrHiringVelocityDto
            {
                AverageTimeToHireDays = 18,
                ChangePercent = -12
            },
            DiversityReport = new HrDiversityReportDto
            {
                TargetCompletionPercent = 85
            }
        });
    }

    public async Task<ApiResponse<ManagerDashboardDto>> GetManagerDashboardAsync()
    {
        IReadOnlyList<Job> pendingApprovalJobs = await _jobRepository.GetPendingApprovalJobsAsync(5);
        IReadOnlyList<Domain.Entities.Application> finalDecisionQueue = await _applicationRepository.GetManagerReviewQueueAsync(null);
        Dictionary<ApplicationStatus, int> statusCounts = await _applicationRepository.GetStatusCountsAsync();
        IReadOnlyList<(string DepartmentName, int AverageDays)> departmentCycleMetrics = await _applicationRepository.GetAverageReviewCycleByDepartmentAsync();
        IReadOnlyList<Department> departments = await _jobRepository.GetDepartmentsAsync();
        int pendingApprovalCount = await _jobRepository.CountPendingApprovalJobsAsync();

        int activeApplications = statusCounts
            .Where(pair =>
                pair.Key != ApplicationStatus.Hired &&
                pair.Key != ApplicationStatus.Rejected &&
                pair.Key != ApplicationStatus.OfferDeclined)
            .Sum(pair => pair.Value);

        int offeredCount = statusCounts.GetValueOrDefault(ApplicationStatus.Offer);
        int acceptedCount = statusCounts.GetValueOrDefault(ApplicationStatus.Hired);
        decimal acceptanceRate = offeredCount + acceptedCount == 0
            ? 0
            : Math.Round((decimal)acceptedCount * 100 / (offeredCount + acceptedCount), 0, MidpointRounding.AwayFromZero);

        int averageReviewCycleDays = departmentCycleMetrics.Count == 0
            ? 0
            : (int)Math.Round(departmentCycleMetrics.Average(metric => metric.AverageDays), MidpointRounding.AwayFromZero);

        return ApiResponse<ManagerDashboardDto>.Ok(new ManagerDashboardDto
        {
            Summary = new ManagerDashboardSummaryDto
            {
                PendingApprovals = pendingApprovalCount,
                ActiveApplications = activeApplications,
                DepartmentCount = departments.Count,
                AverageReviewCycleDays = averageReviewCycleDays,
                AverageReviewCycleLabel = averageReviewCycleDays == 0 ? "Not enough completed interviews" : "Based on completed interview cycles",
                AcceptanceRate = acceptanceRate,
                AcceptanceRateLabel = offeredCount + acceptedCount == 0
                    ? "No offer-stage applications yet"
                    : $"{acceptedCount} accepted from {offeredCount + acceptedCount} offer-stage applications"
            },
            PendingApprovals = pendingApprovalJobs.Select(job => new HrPendingApprovalDto
            {
                JobId = job.Id.ToString(),
                Title = job.Title,
                Meta = BuildPendingApprovalMeta(job),
                ApproverCount = 1
            }).ToList(),
            FinalDecisions = finalDecisionQueue
                .Take(4)
                .Select(application => new ManagerDashboardDecisionItemDto
                {
                    ApplicationId = application.Id.ToString(),
                    CandidateName = application.User.FullName,
                    JobTitle = application.Job.Title,
                    RecommendationNote = application.Interviews
                        .Where(interview => !string.IsNullOrWhiteSpace(interview.Notes))
                        .OrderByDescending(interview => interview.InterviewDate)
                        .Select(interview => interview.Notes!)
                        .FirstOrDefault() ?? "Applications are waiting for manager review.",
                    Status = application.Status.ToString(),
                    AvatarUrl = application.User.AvatarUrl
                })
                .ToList(),
            DepartmentHiringSpeed = departmentCycleMetrics
                .Take(4)
                .Select(metric => new ManagerDashboardDepartmentMetricDto
                {
                    DepartmentName = metric.DepartmentName,
                    AverageDays = metric.AverageDays
                })
                .ToList(),
            RecruitmentFunnel = BuildManagerFunnel(statusCounts)
        });
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

    private static string BuildPendingApprovalMeta(Job job)
    {
        string salaryLabel = job.SalaryMin.HasValue || job.SalaryMax.HasValue
            ? $"{job.SalaryMin:0.#} - {job.SalaryMax:0.#}"
            : "Negotiable";

        return $"{job.Department?.Name ?? "General"} • {salaryLabel}";
    }

    private static List<FunnelCountDto> BuildManagerFunnel(Dictionary<ApplicationStatus, int> statusCounts)
    {
        return
        [
            new FunnelCountDto
            {
                Label = "Sourced",
                Count = statusCounts.Values.Sum()
            },
            new FunnelCountDto
            {
                Label = "Screening",
                Count = statusCounts.GetValueOrDefault(ApplicationStatus.Applied) + statusCounts.GetValueOrDefault(ApplicationStatus.Screening)
            },
            new FunnelCountDto
            {
                Label = "Manager Review",
                Count = statusCounts.GetValueOrDefault(ApplicationStatus.ManagerReview)
            },
            new FunnelCountDto
            {
                Label = "Interview",
                Count = statusCounts.GetValueOrDefault(ApplicationStatus.Interview)
            },
            new FunnelCountDto
            {
                Label = "Offer",
                Count = statusCounts.GetValueOrDefault(ApplicationStatus.Offer)
            }
        ];
    }
}
