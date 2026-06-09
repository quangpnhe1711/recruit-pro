using FluentValidation;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Request.Candidate;

namespace RecruitPro.Application.Validators;

public class UpsertCandidateExperienceRequestValidator : AbstractValidator<UpsertCandidateExperienceRequest>
{
    public UpsertCandidateExperienceRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Company).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Bullets).NotEmpty();
        RuleFor(request => request.Period.StartMonth).InclusiveBetween(1, 12);
        RuleFor(request => request.Period.StartYear).InclusiveBetween(1900, DbDateTime.CurrentYear + 1);
        RuleFor(request => request.Period.EndMonth)
            .InclusiveBetween(1, 12)
            .When(request => !request.Period.IsCurrent && request.Period.EndMonth.HasValue);
        RuleFor(request => request.Period.EndYear)
            .InclusiveBetween(1900, DbDateTime.CurrentYear + 1)
            .When(request => !request.Period.IsCurrent && request.Period.EndYear.HasValue);
    }
}
