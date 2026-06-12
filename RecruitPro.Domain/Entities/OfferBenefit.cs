namespace RecruitPro.Domain.Entities;

public partial class OfferBenefit
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public virtual ICollection<ApplicationOfferBenefit> ApplicationOfferBenefits { get; set; } = new List<ApplicationOfferBenefit>();
}
