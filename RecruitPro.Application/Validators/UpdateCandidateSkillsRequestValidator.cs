using FluentValidation;
using RecruitPro.Application.DTOs.Request.Candidate;

namespace RecruitPro.Application.Validators;

public class UpdateCandidateSkillsRequestValidator : AbstractValidator<UpdateCandidateSkillsRequest>
{
    public UpdateCandidateSkillsRequestValidator()
    {
        RuleFor(request => request)
            .Must(request => request.SkillIds != null || request.Skills != null)
            .WithMessage("At least one skill payload must be provided.");

        RuleForEach(request => request.Skills).SetValidator(new CandidateSkillUpsertRequestValidator());
    }
}
