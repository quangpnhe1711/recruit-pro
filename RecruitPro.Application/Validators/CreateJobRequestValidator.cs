using FluentValidation;
using RecruitPro.Application.DTOs.Request.Jobs;

namespace RecruitPro.Application.Validators;

public class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Department).MaximumLength(100).When(request => !string.IsNullOrWhiteSpace(request.Department));
        RuleFor(request => request.DepartmentId).Must(value => string.IsNullOrWhiteSpace(value) || Guid.TryParse(value, out _));
        RuleFor(request => request.Location).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Description).NotEmpty();
        RuleFor(request => request.Requirements).NotEmpty();
        RuleFor(request => request.VacancyCount).GreaterThan(0);
        RuleFor(request => request.MinExperienceYears).GreaterThanOrEqualTo(0).When(request => request.MinExperienceYears.HasValue);
        RuleFor(request => request.SalaryMax)
            .GreaterThanOrEqualTo(request => request.SalaryMin ?? 0)
            .When(request => request.SalaryMin.HasValue && request.SalaryMax.HasValue);
    }
}
