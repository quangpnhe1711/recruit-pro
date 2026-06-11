namespace RecruitPro.Domain.Entities;

public partial class CopilotCandidateTag
{
    public Guid Id { get; set; }
    public Guid CandidateUserId { get; set; }
    public Guid JobId { get; set; }
    public Guid? RankingSessionId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string TagName { get; set; } = null!;
    public string Source { get; set; } = "AI";
    public DateTime? CreatedAt { get; set; }

    public virtual User CandidateUser { get; set; } = null!;
    public virtual Job Job { get; set; } = null!;
    public virtual CopilotRankingSession? RankingSession { get; set; }
    public virtual User CreatedByUser { get; set; } = null!;
}
