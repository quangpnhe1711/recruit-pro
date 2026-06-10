namespace RecruitPro.Application.DTOs.Request.Jobs;

public class ManagerJobApprovalQueryRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public string? Keyword { get; set; }

    public string? Department { get; set; }
}
