using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories;

public class OfferRepository : IOfferRepository
{
    private readonly AppDbContext _context;

    public OfferRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ApplicationOffer?> GetByApplicationIdAsync(Guid applicationId)
    {
        return BuildOfferQuery()
            .FirstOrDefaultAsync(offer => offer.ApplicationId == applicationId);
    }

    public Task<ApplicationOffer?> GetTrackedByApplicationIdAsync(Guid applicationId)
    {
        return BuildTrackedOfferQuery()
            .FirstOrDefaultAsync(offer => offer.ApplicationId == applicationId);
    }

    public async Task<IReadOnlyList<OfferTemplate>> GetTemplatesAsync()
    {
        return await _context.OfferTemplates
            .AsNoTracking()
            .Where(template => template.IsActive)
            .OrderBy(template => template.DisplayOrder)
            .ThenBy(template => template.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<OfferBenefit>> GetBenefitsAsync()
    {
        return await _context.OfferBenefits
            .AsNoTracking()
            .Where(benefit => benefit.IsActive)
            .OrderBy(benefit => benefit.DisplayOrder)
            .ThenBy(benefit => benefit.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<OfferCurrency>> GetCurrenciesAsync()
    {
        return await _context.OfferCurrencies
            .AsNoTracking()
            .Where(currency => currency.IsActive)
            .OrderBy(currency => currency.DisplayOrder)
            .ThenBy(currency => currency.Code)
            .ToListAsync();
    }

    public async Task AddAsync(ApplicationOffer offer)
    {
        await _context.ApplicationOffers.AddAsync(offer);
    }

    public Task UpdateAsync(ApplicationOffer offer)
    {
        _context.ApplicationOffers.Update(offer);
        return Task.CompletedTask;
    }

    private IQueryable<ApplicationOffer> BuildOfferQuery()
    {
        return _context.ApplicationOffers
            .AsNoTracking()
            .Include(offer => offer.Application)
                .ThenInclude(application => application.User)
            .Include(offer => offer.Application)
                .ThenInclude(application => application.Job)
                    .ThenInclude(job => job.Department)
            .Include(offer => offer.Currency)
            .Include(offer => offer.OfferTemplate)
            .Include(offer => offer.ReportingManagerNavigation)
            .Include(offer => offer.ApplicationOfferBenefits)
                .ThenInclude(link => link.Benefit);
    }

    private IQueryable<ApplicationOffer> BuildTrackedOfferQuery()
    {
        return _context.ApplicationOffers
            .Include(offer => offer.Application)
                .ThenInclude(application => application.User)
            .Include(offer => offer.Application)
                .ThenInclude(application => application.Job)
                    .ThenInclude(job => job.Department)
            .Include(offer => offer.Currency)
            .Include(offer => offer.OfferTemplate)
            .Include(offer => offer.ReportingManagerNavigation)
            .Include(offer => offer.ApplicationOfferBenefits)
                .ThenInclude(link => link.Benefit);
    }
}
