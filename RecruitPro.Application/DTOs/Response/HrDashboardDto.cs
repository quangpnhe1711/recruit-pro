namespace RecruitPro.Application.DTOs.Response;

public class HrDashboardDto
{
    public HrDashboardStatsDto Stats { get; set; } = new();
    public List<HrRecentApplicationDto> RecentApplications { get; set; } = [];
    public List<HrPendingApprovalDto> PendingApprovals { get; set; } = [];
    public HrHiringVelocityDto HiringVelocity { get; set; } = new();
    public HrDiversityReportDto DiversityReport { get; set; } = new();
}

public class HrHiringVelocityDto
{
    public int AverageTimeToHireDays { get; set; }
    public decimal ChangePercent { get; set; }
}

public class HrDiversityReportDto
{
    public int TargetCompletionPercent { get; set; }
}
