using FluentValidation;
using RecruitPro.Application.DTOs.Request.Jobs;

namespace RecruitPro.Application.Validators;

public class ApplyJobRequestValidator : AbstractValidator<ApplyJobRequest>
{
    public ApplyJobRequestValidator()
    {
        RuleFor(request => request.CoverLetter)
            .MaximumLength(2000)
            .When(request => !string.IsNullOrWhiteSpace(request.CoverLetter));
    }
}
