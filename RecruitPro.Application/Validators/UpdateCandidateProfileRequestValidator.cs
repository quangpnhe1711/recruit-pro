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
        RuleForEach(request => request.Skills).SetValidator(new CandidateSkillUpsertRequestValidator());
    }
}

internal sealed class CandidateSkillUpsertRequestValidator : AbstractValidator<CandidateSkillUpsertRequest>
{
    public CandidateSkillUpsertRequestValidator()
    {
        RuleFor(request => request.SkillId).NotEmpty();
        RuleFor(request => request.YearsOfExperience)
            .GreaterThanOrEqualTo(0)
            .When(request => request.YearsOfExperience.HasValue);
    }
}
