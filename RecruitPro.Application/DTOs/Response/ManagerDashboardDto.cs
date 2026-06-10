namespace RecruitPro.Application.DTOs.Response;

public class ManagerDashboardDto
{
    public ManagerDashboardSummaryDto Summary { get; set; } = new();
    public List<HrPendingApprovalDto> PendingApprovals { get; set; } = [];
    public List<ManagerDashboardDecisionItemDto> FinalDecisions { get; set; } = [];
    public List<ManagerDashboardDepartmentMetricDto> DepartmentHiringSpeed { get; set; } = [];
    public List<FunnelCountDto> RecruitmentFunnel { get; set; } = [];
}

public class ManagerDashboardSummaryDto
{
    public int PendingApprovals { get; set; }
    public int ActiveApplications { get; set; }
    public int DepartmentCount { get; set; }
    public int AverageReviewCycleDays { get; set; }
    public string AverageReviewCycleLabel { get; set; } = string.Empty;
    public decimal AcceptanceRate { get; set; }
    public string AcceptanceRateLabel { get; set; } = string.Empty;
}

public class ManagerDashboardDecisionItemDto
{
    public string ApplicationId { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string RecommendationNote { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class ManagerDashboardDepartmentMetricDto
{
    public string DepartmentName { get; set; } = string.Empty;
    public int AverageDays { get; set; }
}
