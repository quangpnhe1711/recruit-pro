namespace RecruitPro.Domain.Entities;

public partial class CopilotSavedRule
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public string RuleJson { get; set; } = "{}";
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public virtual Job Job { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
