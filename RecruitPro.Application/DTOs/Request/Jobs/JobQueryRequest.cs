namespace RecruitPro.Application.DTOs.Request.Jobs;

public class JobQueryRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public string? Keyword { get; set; }

    public List<string> EmploymentTypes { get; set; } = [];

    public List<string> Skills { get; set; } = [];

    public string? SortBy { get; set; }
}
