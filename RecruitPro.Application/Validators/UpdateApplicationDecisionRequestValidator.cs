using FluentValidation;
using RecruitPro.Application.DTOs.Request.Applications;

namespace RecruitPro.Application.Validators;

public class UpdateApplicationDecisionRequestValidator : AbstractValidator<UpdateApplicationDecisionRequest>
{
    private static readonly string[] AllowedDecisions = ["hire", "hold", "reject"];

    public UpdateApplicationDecisionRequestValidator()
    {
        RuleFor(request => request.Decision)
            .NotEmpty()
            .Must(decision => AllowedDecisions.Contains(decision.Trim().ToLowerInvariant()))
            .WithMessage("Decision must be one of: hire, hold, reject.");
    }
}
