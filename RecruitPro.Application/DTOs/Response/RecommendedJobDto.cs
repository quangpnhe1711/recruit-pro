namespace RecruitPro.Application.DTOs.Response;

public class RecommendedJobDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = [];
}
