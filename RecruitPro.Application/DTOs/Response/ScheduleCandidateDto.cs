namespace RecruitPro.Application.DTOs.Response;

public class ScheduleCandidateDto
{
    public string Id { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string AppliedFor { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
