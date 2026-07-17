namespace RecruitPro.Application.DTOs.Response;

/// <summary>
/// Post-interview scorecard as exposed to HR/Manager/HeadDepartment (internal only —
/// never returned on candidate-facing endpoints).
/// </summary>
public class InterviewEvaluationDto
{
    public string InterviewId { get; set; } = string.Empty;
    public string? EvaluatorId { get; set; }
    public string? EvaluatorName { get; set; }
    public int TechnicalScore { get; set; }
    public int CommunicationScore { get; set; }
    public int ProblemSolvingScore { get; set; }
    public int CultureFitScore { get; set; }
    public int OverallScore { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public string? Strengths { get; set; }
    public string? Concerns { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
