namespace RecruitPro.Application.DTOs.Response;

public class ManagerJobApprovalQueueResponseDto
{
    public List<ManagerJobApprovalQueueItemDto> Items { get; set; } = [];
    public ApiEnvelopeMeta Meta { get; set; } = new();
    public ManagerJobApprovalSummaryDto Summary { get; set; } = new();
}

public class ManagerJobApprovalQueueItemDto
{
    public string JobId { get; set; } = string.Empty;
    public string ReferenceCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string HiringTeamLabel { get; set; } = string.Empty;
    public string HrOwnerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public int VacancyCount { get; set; }
    public int RequiredSkillsCount { get; set; }
    public int ApplicationsCount { get; set; }
    public bool IsOverdue { get; set; }
}

public class ManagerJobApprovalSummaryDto
{
    public int PendingApprovals { get; set; }
    public int SubmittedToday { get; set; }
    public int OverdueReviews { get; set; }
    public int DepartmentsWaiting { get; set; }
}
