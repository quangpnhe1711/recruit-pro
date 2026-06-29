using FluentValidation;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Request.Candidate;

namespace RecruitPro.Application.Validators;

public class UpdateCandidateProfileRequestValidator : AbstractValidator<UpdateCandidateProfileRequest>
{
    public UpdateCandidateProfileRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Headline).MaximumLength(100).When(request => !string.IsNullOrWhiteSpace(request.Headline));
        RuleFor(request => request.Phone)
            .Matches(@"^0\d{9}$")
            .When(request => !string.IsNullOrWhiteSpace(request.Phone))
            .WithMessage("Phone must be a 10-digit number starting with 0.");
        RuleFor(request => request.Location).MaximumLength(250).When(request => !string.IsNullOrWhiteSpace(request.Location));
        RuleFor(request => request.Bio).MaximumLength(1000).When(request => !string.IsNullOrWhiteSpace(request.Bio));
        RuleFor(request => request.Github).MaximumLength(500).When(request => !string.IsNullOrWhiteSpace(request.Github));
        RuleFor(request => request.Github).Must(ValidationRuleHelpers.BeAbsoluteHttpUrl).When(request => !string.IsNullOrWhiteSpace(request.Github))
            .WithMessage("GitHub must be a valid absolute URL.");
        RuleFor(request => request.Linkedin).MaximumLength(500).When(request => !string.IsNullOrWhiteSpace(request.Linkedin));
        RuleFor(request => request.Linkedin).Must(ValidationRuleHelpers.BeAbsoluteHttpUrl).When(request => !string.IsNullOrWhiteSpace(request.Linkedin))
            .WithMessage("LinkedIn must be a valid absolute URL.");
        RuleForEach(request => request.Skills).SetValidator(new CandidateSkillUpsertRequestValidator());
        RuleForEach(request => request.ExperienceEntries).SetValidator(new CandidateExperienceUpsertItemRequestValidator());
        RuleForEach(request => request.Projects).SetValidator(new CandidateProjectUpsertRequestValidator());
        RuleForEach(request => request.Educations).SetValidator(new CandidateEducationUpsertRequestValidator());
        RuleForEach(request => request.Certifications).SetValidator(new CandidateCertificationUpsertRequestValidator());
        RuleForEach(request => request.Languages).SetValidator(new CandidateLanguageUpsertRequestValidator());
        RuleForEach(request => request.Sections).SetValidator(new CandidateProfileSectionUpsertRequestValidator());
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

internal sealed class CandidateExperienceUpsertItemRequestValidator : AbstractValidator<CandidateExperienceUpsertItemRequest>
{
    public CandidateExperienceUpsertItemRequestValidator()
    {
        RuleFor(request => request.Title).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Company).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Bullets).NotEmpty();
        RuleForEach(request => request.Bullets).NotEmpty().MaximumLength(500);
        RuleFor(request => request.Period).NotNull();
        RuleFor(request => request.Period.StartMonth).InclusiveBetween(1, 12);
        RuleFor(request => request.Period.StartYear).InclusiveBetween(1900, DbDateTime.CurrentYear + 1);
        RuleFor(request => request.Period.EndMonth)
            .InclusiveBetween(1, 12)
            .When(request => !request.Period.IsCurrent && request.Period.EndMonth.HasValue);
        RuleFor(request => request.Period.EndYear)
            .InclusiveBetween(1900, DbDateTime.CurrentYear + 1)
            .When(request => !request.Period.IsCurrent && request.Period.EndYear.HasValue);
    }
}

internal sealed class CandidateProjectUpsertRequestValidator : AbstractValidator<CandidateProjectUpsertRequest>
{
    public CandidateProjectUpsertRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Role).MaximumLength(255).When(request => !string.IsNullOrWhiteSpace(request.Role));
        RuleFor(request => request.Description).MaximumLength(2000).When(request => !string.IsNullOrWhiteSpace(request.Description));
        RuleForEach(request => request.Technologies).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Period).NotNull();
        RuleFor(request => request.Period.StartMonth).InclusiveBetween(1, 12);
        RuleFor(request => request.Period.StartYear).InclusiveBetween(1900, DbDateTime.CurrentYear + 1);
        RuleFor(request => request.Period.EndMonth)
            .InclusiveBetween(1, 12)
            .When(request => !request.Period.IsCurrent && request.Period.EndMonth.HasValue);
        RuleFor(request => request.Period.EndYear)
            .InclusiveBetween(1900, DbDateTime.CurrentYear + 1)
            .When(request => !request.Period.IsCurrent && request.Period.EndYear.HasValue);
    }
}

internal sealed class CandidateEducationUpsertRequestValidator : AbstractValidator<CandidateEducationUpsertRequest>
{
    public CandidateEducationUpsertRequestValidator()
    {
        RuleFor(request => request.School).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Degree).NotEmpty().MaximumLength(255);
        RuleFor(request => request.FieldOfStudy).MaximumLength(255).When(request => !string.IsNullOrWhiteSpace(request.FieldOfStudy));
        RuleFor(request => request.StartYear)
            .InclusiveBetween(1900, DbDateTime.CurrentYear + 10)
            .When(request => request.StartYear.HasValue);
        RuleFor(request => request.EndYear)
            .InclusiveBetween(1900, DbDateTime.CurrentYear + 10)
            .When(request => request.EndYear.HasValue);
        RuleFor(request => request.Description).MaximumLength(1000).When(request => !string.IsNullOrWhiteSpace(request.Description));
    }
}

internal sealed class CandidateCertificationUpsertRequestValidator : AbstractValidator<CandidateCertificationUpsertRequest>
{
    public CandidateCertificationUpsertRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Issuer).MaximumLength(255).When(request => !string.IsNullOrWhiteSpace(request.Issuer));
        RuleFor(request => request.CredentialId).MaximumLength(255).When(request => !string.IsNullOrWhiteSpace(request.CredentialId));
        RuleFor(request => request.CredentialUrl).MaximumLength(500).When(request => !string.IsNullOrWhiteSpace(request.CredentialUrl));
        RuleFor(request => request.CredentialUrl).Must(ValidationRuleHelpers.BeAbsoluteHttpUrl)
            .When(request => !string.IsNullOrWhiteSpace(request.CredentialUrl))
            .WithMessage("Credential URL must be a valid absolute URL.");
        RuleFor(request => request)
            .Must(request => !request.IssuedOn.HasValue || !request.ExpiresOn.HasValue || request.ExpiresOn.Value.Date >= request.IssuedOn.Value.Date)
            .WithMessage("Expiration date must be on or after issued date.");
    }
}

internal static class ValidationRuleHelpers
{
    public static bool BeAbsoluteHttpUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

internal sealed class CandidateLanguageUpsertRequestValidator : AbstractValidator<CandidateLanguageUpsertRequest>
{
    public CandidateLanguageUpsertRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Proficiency).NotEmpty().MaximumLength(100);
    }
}

internal sealed class CandidateProfileSectionUpsertRequestValidator : AbstractValidator<CandidateProfileSectionUpsertRequest>
{
    public CandidateProfileSectionUpsertRequestValidator()
    {
        RuleFor(request => request.SectionKey).MaximumLength(100).When(request => !string.IsNullOrWhiteSpace(request.SectionKey));
        RuleFor(request => request.Title).NotEmpty().MaximumLength(255);
        RuleFor(request => request.SectionType).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Source).NotEmpty().MaximumLength(100);
        RuleForEach(request => request.Items).SetValidator(new CandidateProfileSectionItemUpsertRequestValidator());
    }
}

internal sealed class CandidateProfileSectionItemUpsertRequestValidator : AbstractValidator<CandidateProfileSectionItemUpsertRequest>
{
    public CandidateProfileSectionItemUpsertRequestValidator()
    {
        RuleFor(request => request.ItemType).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(255);
        RuleFor(request => request.Subtitle).MaximumLength(255).When(request => !string.IsNullOrWhiteSpace(request.Subtitle));
        RuleFor(request => request.Organization).MaximumLength(255).When(request => !string.IsNullOrWhiteSpace(request.Organization));
        RuleFor(request => request.Location).MaximumLength(255).When(request => !string.IsNullOrWhiteSpace(request.Location));
        RuleFor(request => request.Description).MaximumLength(2000).When(request => !string.IsNullOrWhiteSpace(request.Description));
        RuleFor(request => request.DateLabel).MaximumLength(100).When(request => !string.IsNullOrWhiteSpace(request.DateLabel));
        RuleFor(request => request.StartMonth).InclusiveBetween(1, 12).When(request => request.StartMonth.HasValue);
        RuleFor(request => request.EndMonth).InclusiveBetween(1, 12).When(request => request.EndMonth.HasValue);
        RuleFor(request => request.StartYear).InclusiveBetween(1900, DbDateTime.CurrentYear + 10).When(request => request.StartYear.HasValue);
        RuleFor(request => request.EndYear).InclusiveBetween(1900, DbDateTime.CurrentYear + 10).When(request => request.EndYear.HasValue);
        RuleForEach(request => request.Tags).NotEmpty().MaximumLength(100);
    }
}
