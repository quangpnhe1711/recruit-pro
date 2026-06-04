namespace RecruitPro.Application.DTOs.Response;

public class HrJobListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string CreatedDate { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public int ApplicationsCount { get; set; }
}
