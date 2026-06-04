namespace RecruitPro.Application.DTOs.Request.Interviews;

public class CreateInterviewRequest
{
    public string CandidateId { get; set; } = string.Empty;

    public string ApplicationId { get; set; } = string.Empty;

    public string JobId { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public int StartMinutes { get; set; }

    public int DurationMinutes { get; set; }

    public string Mode { get; set; } = "video";

    public string? LocationOrLink { get; set; }

    public string? InterviewerId { get; set; }

    public string? Status { get; set; }
}
