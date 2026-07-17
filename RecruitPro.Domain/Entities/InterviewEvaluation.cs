using RecruitPro.Domain.Enums;

namespace RecruitPro.Domain.Entities;

/// <summary>
/// Post-interview scorecard (one per interview). Recorded by HR/Manager after the interview is
/// Completed; feeds the Interview → Offer / Rejected decision with structured evidence instead of
/// a bare Completed status.
/// </summary>
public partial class InterviewEvaluation
{
    public Guid Id { get; set; }

    public Guid InterviewId { get; set; }

    public Guid? EvaluatorId { get; set; }

    // Criteria scores, each 1..5.
    public int TechnicalScore { get; set; }

    public int CommunicationScore { get; set; }

    public int ProblemSolvingScore { get; set; }

    public int CultureFitScore { get; set; }

    // Derived 0..100 (criteria sum / 20 * 100) — persisted for list views and reporting.
    public int OverallScore { get; set; }

    public InterviewRecommendation Recommendation { get; set; }

    public string? Strengths { get; set; }

    public string? Concerns { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Interview Interview { get; set; } = null!;

    public virtual User? Evaluator { get; set; }
}
