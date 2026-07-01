namespace RecruitPro.Domain.Entities;

public partial class CopilotRankingSession
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public string UserPrompt { get; set; } = null!;
    public string NormalizedRulesJson { get; set; } = "{}";

    // v2 idempotency fingerprint (SHA-256 hex) over the effective ranking input: job, user,
    // normalized prompt/criteria/rules and the screening candidate evidence. A repeated rank with an
    // unchanged fingerprint returns this session instead of re-running the AI provider.
    public string? InputHash { get; set; }

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
