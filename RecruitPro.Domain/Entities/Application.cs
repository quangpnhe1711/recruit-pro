using System;
using System.Collections.Generic;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Domain.Entities;

public partial class Application
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid JobId { get; set; }

    public Guid? ReviewedBy { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Applied;

    public DateTime? AppliedAt { get; set; }

    public string? CoverLetter { get; set; }

    public decimal? RuleScore { get; set; }

    public decimal? SemanticScore { get; set; }

    public decimal? FinalScore { get; set; }

    public string? ScoreStatus { get; set; }

    public string? ScoreError { get; set; }

    public DateTime? ScoredAt { get; set; }

    public virtual ApplicationOffer? Offer { get; set; }

    public virtual ICollection<Interview> Interviews { get; set; } = new List<Interview>();

    public virtual Job Job { get; set; } = null!;

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual User User { get; set; } = null!;
}
