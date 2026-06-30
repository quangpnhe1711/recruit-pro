namespace RecruitPro.Domain.Entities;

public partial class CopilotPromptTemplate
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = null!;
    public string TemplateType { get; set; } = null!;
    public string Prompt { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual User OwnerUser { get; set; } = null!;
}
