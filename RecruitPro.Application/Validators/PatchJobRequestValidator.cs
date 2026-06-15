using FluentValidation;
using RecruitPro.Application.DTOs.Request.Jobs;

namespace RecruitPro.Application.Validators;

public class PatchJobRequestValidator : AbstractValidator<PatchJobRequest>
{
    public PatchJobRequestValidator()
    {
        RuleFor(request => request.Title).MaximumLength(255).When(request => !string.IsNullOrWhiteSpace(request.Title));
        RuleFor(request => request.Department).MaximumLength(100).When(request => !string.IsNullOrWhiteSpace(request.Department));
        RuleFor(request => request.DepartmentId).Must(value => string.IsNullOrWhiteSpace(value) || Guid.TryParse(value, out _));
        RuleFor(request => request).Must(request =>
                !string.IsNullOrWhiteSpace(request.Title) ||
                !string.IsNullOrWhiteSpace(request.DepartmentId) ||
                !string.IsNullOrWhiteSpace(request.Department) ||
                !string.IsNullOrWhiteSpace(request.ApprovalStatus) ||
                !string.IsNullOrWhiteSpace(request.Description) ||
                request.Requirements != null ||
                request.Benefits != null ||
                !string.IsNullOrWhiteSpace(request.Location) ||
                !string.IsNullOrWhiteSpace(request.WorkMode) ||
                !string.IsNullOrWhiteSpace(request.EmploymentType) ||
                request.MinExperienceYears.HasValue ||
                request.VacancyCount.HasValue ||
                request.SalaryMin.HasValue ||
                request.SalaryMax.HasValue ||
                request.Deadline.HasValue ||
                request.SkillIds != null ||
                request.Skills != null ||
                request.SkillRequirements != null)
            .WithMessage("At least one field must be provided.");
    }
}
