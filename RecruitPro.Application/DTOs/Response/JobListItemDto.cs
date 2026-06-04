namespace RecruitPro.Application.DTOs.Response;

public class JobListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ShortPitch { get; set; }
    public string Department { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string WorkMode { get; set; } = string.Empty;
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? PostedAt { get; set; }
    public List<string> Tags { get; set; } = [];
    public string ShortDescription { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
}
