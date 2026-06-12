namespace RecruitPro.Domain.Entities;

public partial class OfferTemplate
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string TemplateBody { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public virtual ICollection<ApplicationOffer> ApplicationOffers { get; set; } = new List<ApplicationOffer>();
}
