namespace RecruitPro.Application.DTOs.Request.Jobs;

public class HrJobQueryRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public string? Department { get; set; }

    public string? ApprovalStatus { get; set; }

    public string? CreatedByUserId { get; set; }
}
