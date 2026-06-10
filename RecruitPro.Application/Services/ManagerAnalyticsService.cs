using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Services;

public class ManagerAnalyticsService : IManagerAnalyticsService
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IInterviewRepository _interviewRepository;
    private readonly IJobRepository _jobRepository;

    public ManagerAnalyticsService(
        IApplicationRepository applicationRepository,
        IInterviewRepository interviewRepository,
        IJobRepository jobRepository)
    {
        _applicationRepository = applicationRepository;
        _interviewRepository = interviewRepository;
        _jobRepository = jobRepository;
    }

    public async Task<ApiResponse<ManagerRecruitmentAnalyticsDto>> GetRecruitmentAnalyticsAsync()
    {
        DateTime currentMonth = new DateTime(DbDateTime.Now.Year, DbDateTime.Now.Month, 1);
        DateTime startMonth = currentMonth.AddMonths(-5);

        Dictionary<ApplicationStatus, int> statusCounts = await _applicationRepository.GetStatusCountsAsync();
        double? averageReviewCycle = await _applicationRepository.GetAverageReviewCycleDaysAsync();
        int activeCandidates = await _applicationRepository.CountActiveCandidatesAsync();
        int pendingInterviews = await _interviewRepository.CountUpcomingScheduledAsync(DbDateTime.Now);
        IReadOnlyList<(DateTime Month, int Count)> monthlyApplications = await _applicationRepository.GetMonthlyApplicationVolumeAsync(startMonth, 6);
        IReadOnlyList<(DateTime Month, int Count)> monthlyCompletedInterviews = await _interviewRepository.GetMonthlyCompletedVolumeAsync(startMonth, 6);
        IReadOnlyList<(string DepartmentName, int ActiveApplications, int OfferedCandidates, int AcceptedCandidates)> departmentPipeline = await _applicationRepository.GetDepartmentPipelineSnapshotAsync();
        IReadOnlyList<(string DepartmentName, int AverageDays)> departmentReviewCycles = await _applicationRepository.GetAverageReviewCycleByDepartmentAsync();
        IReadOnlyList<(string DepartmentName, int OpenRoles, string RecruiterName)> departmentOpenRoles = await _jobRepository.GetDepartmentOpenRoleSnapshotAsync();

        int offeredCount = statusCounts.GetValueOrDefault(ApplicationStatus.ManagerReview);
        int acceptedCount = statusCounts.GetValueOrDefault(ApplicationStatus.Accepted);
        int offerAcceptanceRate = offeredCount + acceptedCount == 0
            ? 0
            : (int)Math.Round((decimal)acceptedCount * 100 / (offeredCount + acceptedCount), MidpointRounding.AwayFromZero);

        List<DateTime> months = Enumerable.Range(0, 6)
            .Select(offset => startMonth.AddMonths(offset))
            .ToList();

        List<int> applicationSeries = months
            .Select(month => monthlyApplications.FirstOrDefault(item => item.Month.Year == month.Year && item.Month.Month == month.Month).Count)
            .ToList();
        List<int> completedInterviewSeries = months
            .Select(month => monthlyCompletedInterviews.FirstOrDefault(item => item.Month.Year == month.Year && item.Month.Month == month.Month).Count)
            .ToList();

        int previousApplications = applicationSeries.Take(3).Sum();
        int currentApplications = applicationSeries.Skip(3).Sum();
        int previousPendingInterviews = completedInterviewSeries.Take(3).Sum();
        int currentPendingInterviews = completedInterviewSeries.Skip(3).Sum();

        int reviewStageCount = statusCounts.GetValueOrDefault(ApplicationStatus.Pending) + statusCounts.GetValueOrDefault(ApplicationStatus.Reviewing);
        int interviewStageCount = statusCounts.GetValueOrDefault(ApplicationStatus.Interviewing);
        int finalReviewCount = statusCounts.GetValueOrDefault(ApplicationStatus.ManagerReview);
        int acceptedStageCount = statusCounts.GetValueOrDefault(ApplicationStatus.Accepted);
        int distributionTotal = reviewStageCount + interviewStageCount + finalReviewCount + acceptedStageCount;

        List<ManagerRecruitmentDistributionItemDto> distributionItems =
        [
            BuildDistributionItem("Review Stage", reviewStageCount, distributionTotal, "primary"),
            BuildDistributionItem("Interview Stage", interviewStageCount, distributionTotal, "tertiary"),
            BuildDistributionItem("Final Review", finalReviewCount, distributionTotal, "secondary"),
            BuildDistributionItem("Accepted", acceptedStageCount, distributionTotal, "surface")
        ];

        List<ManagerDepartmentBreakdownDto> departmentBreakdown = departmentOpenRoles
            .Select(item =>
            {
                (string DepartmentName, int ActiveApplications, int OfferedCandidates, int AcceptedCandidates) pipelineRow =
                    departmentPipeline.FirstOrDefault(metric => metric.DepartmentName.Equals(item.DepartmentName, StringComparison.OrdinalIgnoreCase));
                (string DepartmentName, int AverageDays) reviewCycleRow =
                    departmentReviewCycles.FirstOrDefault(metric => metric.DepartmentName.Equals(item.DepartmentName, StringComparison.OrdinalIgnoreCase));

                return new ManagerDepartmentBreakdownDto
                {
                    DepartmentName = item.DepartmentName,
                    OpenRoles = item.OpenRoles,
                    AverageReviewCycleDays = reviewCycleRow.AverageDays,
                    ActivePipeline = pipelineRow.ActiveApplications,
                    RecruiterName = item.RecruiterName
                };
            })
            .OrderByDescending(item => item.OpenRoles)
            .ThenBy(item => item.DepartmentName)
            .ToList();

        return ApiResponse<ManagerRecruitmentAnalyticsDto>.Ok(new ManagerRecruitmentAnalyticsDto
        {
            Overview = new ManagerRecruitmentOverviewDto
            {
                AverageReviewCycleDays = averageReviewCycle.HasValue
                    ? (int)Math.Round(averageReviewCycle.Value, MidpointRounding.AwayFromZero)
                    : 0,
                AverageReviewCycleDeltaPercent = CalculatePercentDelta(previousApplications, currentApplications) * -1,
                ActiveCandidates = activeCandidates,
                ActiveCandidatesDelta = currentApplications - previousApplications,
                PendingInterviews = pendingInterviews,
                PendingInterviewsDelta = currentPendingInterviews - previousPendingInterviews,
                OfferAcceptanceRate = offerAcceptanceRate,
                OfferAcceptanceDeltaPercent = acceptedCount - offeredCount
            },
            Trend = new ManagerRecruitmentTrendDto
            {
                Labels = months.Select(month => month.ToString("MMM").ToUpperInvariant()).ToList(),
                Applications = applicationSeries,
                CompletedInterviews = completedInterviewSeries
            },
            Funnel =
            [
                BuildFunnelItem("Applied", statusCounts.Values.Sum(), statusCounts.Values.Sum()),
                BuildFunnelItem("Screened", reviewStageCount, statusCounts.Values.Sum()),
                BuildFunnelItem("Interview", interviewStageCount, statusCounts.Values.Sum()),
                BuildFunnelItem("Offered", finalReviewCount, statusCounts.Values.Sum()),
                BuildFunnelItem("Accepted", acceptedStageCount, statusCounts.Values.Sum())
            ],
            Distribution = new ManagerRecruitmentDistributionDto
            {
                Total = distributionTotal,
                Items = distributionItems.Where(item => item.Count > 0).ToList()
            },
            DepartmentPerformance = departmentPipeline
                .OrderByDescending(item => item.ActiveApplications)
                .ThenBy(item => item.DepartmentName)
                .Select(item => new ManagerDepartmentPipelineDto
                {
                    DepartmentName = item.DepartmentName,
                    ActiveApplications = item.ActiveApplications,
                    OfferedCandidates = item.OfferedCandidates,
                    AcceptedCandidates = item.AcceptedCandidates,
                    ConversionPercent = item.ActiveApplications == 0
                        ? 0
                        : (int)Math.Round((decimal)item.AcceptedCandidates * 100 / item.ActiveApplications, MidpointRounding.AwayFromZero)
                })
                .ToList(),
            DepartmentBreakdown = departmentBreakdown
        });
    }

    private static ManagerRecruitmentFunnelItemDto BuildFunnelItem(string label, int count, int appliedCount)
    {
        return new ManagerRecruitmentFunnelItemDto
        {
            Label = label,
            Count = count,
            PercentFromApplied = appliedCount == 0
                ? 0
                : (int)Math.Round((decimal)count * 100 / appliedCount, MidpointRounding.AwayFromZero)
        };
    }

    private static ManagerRecruitmentDistributionItemDto BuildDistributionItem(string label, int count, int total, string colorToken)
    {
        return new ManagerRecruitmentDistributionItemDto
        {
            Label = label,
            Count = count,
            Percent = total == 0 ? 0 : (int)Math.Round((decimal)count * 100 / total, MidpointRounding.AwayFromZero),
            ColorToken = colorToken
        };
    }

    private static int CalculatePercentDelta(int previous, int current)
    {
        if (previous <= 0)
        {
            return current > 0 ? 100 : 0;
        }

        return (int)Math.Round(((decimal)(current - previous) * 100) / previous, MidpointRounding.AwayFromZero);
    }
}
