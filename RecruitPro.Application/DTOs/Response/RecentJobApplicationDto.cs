namespace RecruitPro.Application.DTOs.Response;

public class RecentJobApplicationDto
{
    public string Id { get; set; } = string.Empty;
    public string CandidateId { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? Score { get; set; }
}
