namespace RecruitPro.Application.DTOs.Response;

public class CandidateApplicationListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string CompanyOrDepartment { get; set; } = string.Empty;
    public DateTime? AppliedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string NextStep { get; set; } = string.Empty;
    public List<string> AvailableActions { get; set; } = [];
}
