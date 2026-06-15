namespace RecruitPro.Application.DTOs.Request.Applications;

public class HrApplicationQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Keyword { get; set; }
    public string? Department { get; set; }
    public string? Status { get; set; }
    public string? JobId { get; set; }
}
