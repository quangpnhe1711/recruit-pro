using FluentValidation;
using RecruitPro.Application.DTOs.Request;

namespace RecruitPro.Application.Validators;

public class SendApplicationEmailRequestValidator : AbstractValidator<SendApplicationEmailRequest>
{
    public SendApplicationEmailRequestValidator()
    {
        RuleFor(request => request.TemplateType).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Subject).MaximumLength(255).When(request => !string.IsNullOrWhiteSpace(request.Subject));
    }
}
