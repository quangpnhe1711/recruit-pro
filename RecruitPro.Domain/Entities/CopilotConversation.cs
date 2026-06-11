namespace RecruitPro.Domain.Entities;

public partial class CopilotConversation
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = "Active";
    public Guid? LatestRankingSessionId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Job Job { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual CopilotRankingSession? LatestRankingSession { get; set; }
    public virtual ICollection<CopilotMessage> Messages { get; set; } = new List<CopilotMessage>();
    public virtual ICollection<CopilotRankingSession> RankingSessions { get; set; } = new List<CopilotRankingSession>();
}
