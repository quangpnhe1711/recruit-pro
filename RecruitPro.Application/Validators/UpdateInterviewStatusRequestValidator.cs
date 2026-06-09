using FluentValidation;
using RecruitPro.Application.DTOs.Request.Interviews;

namespace RecruitPro.Application.Validators;

public class UpdateInterviewStatusRequestValidator : AbstractValidator<UpdateInterviewStatusRequest>
{
    public UpdateInterviewStatusRequestValidator()
    {
        RuleFor(request => request.Status).NotEmpty();
    }
}
