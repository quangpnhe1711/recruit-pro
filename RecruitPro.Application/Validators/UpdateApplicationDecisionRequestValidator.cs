using FluentValidation;
using RecruitPro.Application.DTOs.Request.Applications;

namespace RecruitPro.Application.Validators;

public class UpdateApplicationDecisionRequestValidator : AbstractValidator<UpdateApplicationDecisionRequest>
{
    private static readonly string[] AllowedStatuses =
    [
        "screening",
        "managerreview",
        "interview",
        "offer",
        "rejected",
    ];

    public UpdateApplicationDecisionRequestValidator()
    {
        RuleFor(request => request.TargetStatus)
            .NotEmpty()
            .Must(status => AllowedStatuses.Contains(status.Trim().ToLowerInvariant()))
            .WithMessage("TargetStatus must be one of: Screening, ManagerReview, Interview, Offer, Rejected.");
    }
}
