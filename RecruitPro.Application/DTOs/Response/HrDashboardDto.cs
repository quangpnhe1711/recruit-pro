namespace RecruitPro.Application.DTOs.Response;

public class HrDashboardDto
{
    public HrDashboardStatsDto Stats { get; set; } = new();
    public List<HrRecentApplicationDto> RecentApplications { get; set; } = [];
    public List<HrPendingApprovalDto> PendingApprovals { get; set; } = [];
}
