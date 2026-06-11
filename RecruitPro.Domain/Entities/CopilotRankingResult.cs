namespace RecruitPro.Domain.Entities;

public partial class CopilotRankingResult
{
    public Guid Id { get; set; }
    public Guid RankingSessionId { get; set; }
    public Guid CandidateUserId { get; set; }
    public Guid ApplicationId { get; set; }
    public int RankPosition { get; set; }
    public decimal TotalScore { get; set; }
    public decimal SkillScore { get; set; }
    public decimal ExperienceScore { get; set; }
    public decimal EducationScore { get; set; }
    public decimal ProjectScore { get; set; }
    public string Recommendation { get; set; } = null!;
    public string? RejectReason { get; set; }
    public bool IsAutoRejected { get; set; }
    public string StrengthsJson { get; set; } = "[]";
    public string WeaknessesJson { get; set; } = "[]";
    public string? ExplanationJson { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual CopilotRankingSession RankingSession { get; set; } = null!;
    public virtual User CandidateUser { get; set; } = null!;
    public virtual Application Application { get; set; } = null!;
}
