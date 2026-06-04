namespace RecruitPro.Application.DTOs.Response;

public class HrCandidateListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Source { get; set; } = "Portal";
    public string AppliedDate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
