namespace RecruitPro.Domain.Entities;

public partial class OfferCurrency
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public virtual ICollection<ApplicationOffer> ApplicationOffers { get; set; } = new List<ApplicationOffer>();
}
