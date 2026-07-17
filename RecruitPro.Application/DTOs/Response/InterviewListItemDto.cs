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
    // Meeting logistics so the candidate (and HR) can actually reach the interview:
    // "Online" | "Offline" | "" plus the corresponding link or address.
    public string MeetingType { get; set; } = string.Empty;
    public string? MeetingLink { get; set; }
    public string? Location { get; set; }
    // When the candidate confirmed attendance (interview:confirm-own). Null = not confirmed.
    public DateTime? CandidateConfirmedAt { get; set; }
    // Scorecard summary — INTERNAL ONLY. Filled by the HR list path (GetInterviewsAsync) after
    // mapping; deliberately never populated on candidate-facing endpoints.
    public int? EvaluationOverallScore { get; set; }
    public string? EvaluationRecommendation { get; set; }
}
