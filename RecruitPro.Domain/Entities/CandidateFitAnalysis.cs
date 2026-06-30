namespace RecruitPro.Domain.Entities;

public partial class CandidateFitAnalysis
{
    public Guid Id { get; set; }
    public Guid AuditId { get; set; }
    public Guid JobId { get; set; }
    public Guid CandidateUserId { get; set; }
    public Guid ApplicationId { get; set; }
    public string FitLabel { get; set; } = null!;
    public decimal ConfidenceScore { get; set; }
    public decimal TotalScore { get; set; }
    public string StrengthsJson { get; set; } = "[]";
    public string GapsJson { get; set; } = "[]";
    public string EvidenceJson { get; set; } = "[]";
    public string Summary { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public bool FallbackUsed { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual Job Job { get; set; } = null!;
    public virtual User CandidateUser { get; set; } = null!;
    public virtual Application Application { get; set; } = null!;
}
