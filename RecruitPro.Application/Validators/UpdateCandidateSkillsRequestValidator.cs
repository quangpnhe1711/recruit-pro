using FluentValidation;
using RecruitPro.Application.DTOs.Request.Candidate;

namespace RecruitPro.Application.Validators;

public class UpdateCandidateSkillsRequestValidator : AbstractValidator<UpdateCandidateSkillsRequest>
{
    public UpdateCandidateSkillsRequestValidator()
    {
        RuleFor(request => request.SkillIds).NotNull();
    }
}
