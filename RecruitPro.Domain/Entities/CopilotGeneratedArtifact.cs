namespace RecruitPro.Domain.Entities;

public partial class CopilotGeneratedArtifact
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid? JobId { get; set; }
    public Guid? ApplicationId { get; set; }
    public string ArtifactType { get; set; } = null!;
    public string Prompt { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public bool FallbackUsed { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual User OwnerUser { get; set; } = null!;
    public virtual Job? Job { get; set; }
    public virtual Application? Application { get; set; }
}
