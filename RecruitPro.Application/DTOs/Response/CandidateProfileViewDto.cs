namespace RecruitPro.Application.DTOs.Response;

public class CandidateProfileViewDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Headline { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Location { get; set; } = string.Empty;
    public string MemberSince { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? Github { get; set; }
    public string? Linkedin { get; set; }
}
