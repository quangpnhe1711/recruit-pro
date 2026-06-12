namespace RecruitPro.Domain.Entities;

public partial class ApplicationOfferBenefit
{
    public Guid OfferId { get; set; }

    public Guid BenefitId { get; set; }

    public virtual ApplicationOffer Offer { get; set; } = null!;

    public virtual OfferBenefit Benefit { get; set; } = null!;
}
