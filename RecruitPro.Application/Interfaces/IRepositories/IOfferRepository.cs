using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories;

public interface IOfferRepository
{
    Task<ApplicationOffer?> GetByApplicationIdAsync(Guid applicationId);
    Task<ApplicationOffer?> GetTrackedByApplicationIdAsync(Guid applicationId);
    Task<IReadOnlyList<OfferTemplate>> GetTemplatesAsync();
    Task<IReadOnlyList<OfferBenefit>> GetBenefitsAsync();
    Task<IReadOnlyList<OfferCurrency>> GetCurrenciesAsync();
    Task AddAsync(ApplicationOffer offer);
    Task UpdateAsync(ApplicationOffer offer);
}
