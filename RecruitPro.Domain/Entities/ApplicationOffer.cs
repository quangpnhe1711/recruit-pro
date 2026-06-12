using RecruitPro.Domain.Enums;

namespace RecruitPro.Domain.Entities;

public partial class ApplicationOffer
{
    public Guid Id { get; set; }

    public Guid ApplicationId { get; set; }

    public Guid? OfferTemplateId { get; set; }

    public decimal BaseSalary { get; set; }

    public string CurrencyCode { get; set; } = "USD";

    public string? BonusDescription { get; set; }

    public string? EquityNotes { get; set; }

    public string EmploymentType { get; set; } = string.Empty;

    public DateTime? ProposedStartDate { get; set; }

    public string? ProbationPeriod { get; set; }

    public Guid? ReportingManagerId { get; set; }

    public string? PersonalMessage { get; set; }

    public OfferStatus Status { get; set; } = OfferStatus.Draft;

    public DateTime? SentAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Application Application { get; set; } = null!;

    public virtual OfferCurrency Currency { get; set; } = null!;

    public virtual OfferTemplate? OfferTemplate { get; set; }

    public virtual User? ReportingManagerNavigation { get; set; }

    public virtual ICollection<ApplicationOfferBenefit> ApplicationOfferBenefits { get; set; } = new List<ApplicationOfferBenefit>();
}
