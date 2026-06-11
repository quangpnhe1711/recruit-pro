namespace RecruitPro.Domain.Entities;

public partial class CopilotMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string? MetadataJson { get; set; }
    public int SequenceNo { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual CopilotConversation Conversation { get; set; } = null!;
}
