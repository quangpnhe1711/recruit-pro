namespace RecruitPro.Application.DTOs.Response;

public class JobDetailScreenDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime? PostedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public SalaryRangeDto SalaryRange { get; set; } = new();
    public string Department { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public int? VacancyCount { get; set; }
    public List<string> Description { get; set; } = [];
    public List<string> Requirements { get; set; } = [];
    public ApplicationSummaryDto ApplicationSummary { get; set; } = new();
}
