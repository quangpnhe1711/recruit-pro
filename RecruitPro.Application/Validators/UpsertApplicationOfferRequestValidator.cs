using FluentValidation;
using RecruitPro.Application.DTOs.Request.Offers;

namespace RecruitPro.Application.Validators;

public class UpsertApplicationOfferRequestValidator : AbstractValidator<UpsertApplicationOfferRequest>
{
    public UpsertApplicationOfferRequestValidator()
    {
        RuleFor(request => request.OfferTemplateId)
            .Must(value => string.IsNullOrWhiteSpace(value) || Guid.TryParse(value, out _))
            .WithMessage("Offer template id is invalid.");
        RuleFor(request => request.BaseSalary)
            .GreaterThan(0);
        RuleFor(request => request.CurrencyCode)
            .NotEmpty()
            .MaximumLength(10);
        RuleFor(request => request.BonusDescription)
            .MaximumLength(500)
            .When(request => !string.IsNullOrWhiteSpace(request.BonusDescription));
        RuleFor(request => request.EquityNotes)
            .MaximumLength(500)
            .When(request => !string.IsNullOrWhiteSpace(request.EquityNotes));
        RuleFor(request => request.EmploymentType)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(request => request.ProbationPeriod)
            .MaximumLength(100)
            .When(request => !string.IsNullOrWhiteSpace(request.ProbationPeriod));
        RuleFor(request => request.ReportingManagerId)
            .Must(value => string.IsNullOrWhiteSpace(value) || Guid.TryParse(value, out _))
            .WithMessage("Reporting manager id is invalid.");
        RuleFor(request => request.PersonalMessage)
            .MaximumLength(2000)
            .When(request => !string.IsNullOrWhiteSpace(request.PersonalMessage));
        RuleForEach(request => request.BenefitIds)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("Benefit id is invalid.");
    }
}
