namespace RecruitPro.Application.DTOs.Response;

public class InterviewListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Interviewer { get; set; } = string.Empty;
    public string DateLabel { get; set; } = string.Empty;
    public string TimeLabel { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
