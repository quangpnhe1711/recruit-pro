namespace RecruitPro.Domain.Entities;

public partial class CopilotRankingSession
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public string UserPrompt { get; set; } = null!;
    public string NormalizedRulesJson { get; set; } = "{}";
    public int TotalCandidates { get; set; }
    public string? ModelName { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual Job Job { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual CopilotConversation Conversation { get; set; } = null!;
    public virtual ICollection<CopilotRankingResult> Results { get; set; } = new List<CopilotRankingResult>();
}
