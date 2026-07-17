namespace RecruitPro.Application.DTOs.Request.Interviews;

/// <summary>
/// Scorecard payload recorded by HR/Manager after an interview is Completed.
/// Criteria scores are 1..5; the overall 0..100 score is derived server-side.
/// </summary>
public class UpsertInterviewEvaluationRequest
{
    public int TechnicalScore { get; set; }
    public int CommunicationScore { get; set; }
    public int ProblemSolvingScore { get; set; }
    public int CultureFitScore { get; set; }
    /// <summary>StrongHire | Hire | NoHire | StrongNoHire.</summary>
    public string Recommendation { get; set; } = string.Empty;
    public string? Strengths { get; set; }
    public string? Concerns { get; set; }
    public string? Notes { get; set; }
}
