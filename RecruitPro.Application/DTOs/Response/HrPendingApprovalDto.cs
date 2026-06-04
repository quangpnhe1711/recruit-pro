namespace RecruitPro.Application.DTOs.Response;

public class HrPendingApprovalDto
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public int ApproverCount { get; set; }
}
