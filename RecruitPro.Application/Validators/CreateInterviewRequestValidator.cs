using FluentValidation;
using RecruitPro.Application.DTOs.Request.Interviews;

namespace RecruitPro.Application.Validators;

public class CreateInterviewRequestValidator : AbstractValidator<CreateInterviewRequest>
{
    public CreateInterviewRequestValidator()
    {
        RuleFor(request => request.ApplicationId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _));
        RuleFor(request => request.CandidateId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _));
        RuleFor(request => request.JobId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _));
        RuleFor(request => request.Date).NotEmpty();
        RuleFor(request => request.StartMinutes).InclusiveBetween(0, 1439);
        RuleFor(request => request.DurationMinutes).InclusiveBetween(15, 240);
        RuleFor(request => request.Mode).NotEmpty();
        RuleFor(request => request.LocationOrLink).NotEmpty();
        RuleFor(request => request.InterviewerId)
            .Must(value => string.IsNullOrWhiteSpace(value) || Guid.TryParse(value, out _));
    }
}
