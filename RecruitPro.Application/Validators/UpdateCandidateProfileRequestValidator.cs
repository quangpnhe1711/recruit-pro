using FluentValidation;
using RecruitPro.Application.DTOs.Request.Candidate;

namespace RecruitPro.Application.Validators;

public class UpdateCandidateProfileRequestValidator : AbstractValidator<UpdateCandidateProfileRequest>
{
    public UpdateCandidateProfileRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Phone).MaximumLength(20).When(request => !string.IsNullOrWhiteSpace(request.Phone));
    }
}
