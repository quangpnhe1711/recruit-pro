using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Request.Applications;
using RecruitPro.Application.DTOs.Request.Candidate;
using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.Validators;

namespace RecruitPro.Tests;

public sealed class ValidatorTests
{
    [Fact]
    public void ApplyJobRequestValidator_AllowsEmptyCoverLetter()
    {
        var validator = new ApplyJobRequestValidator();
        var request = new ApplyJobRequest();

        validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ApplyJobRequestValidator_RejectsTooLongCoverLetter()
    {
        var validator = new ApplyJobRequestValidator();
        var request = new ApplyJobRequest { CoverLetter = new string('a', 2001) };

        validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateJobRequestValidator_AcceptsValidPayload()
    {
        var validator = new CreateJobRequestValidator();
        var request = new CreateJobRequest
        {
            Title = "Backend Engineer",
            DepartmentId = Guid.NewGuid().ToString(),
            Department = "Engineering",
            Location = "HCMC",
            Description = "Build APIs",
            Requirements = ["C#", ".NET"],
            VacancyCount = 2,
            MinExperienceYears = 2,
            SalaryMin = 1000,
            SalaryMax = 2000,
            SkillRequirements =
            [
                new JobSkillRequirementRequest
                {
                    SkillName = "C#",
                    SkillType = "Required",
                    MinimumYearsOfExperience = 1
                }
            ]
        };

        validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateJobRequestValidator_RejectsInvalidDepartmentId_AndSalaryRange()
    {
        var validator = new CreateJobRequestValidator();
        var request = new CreateJobRequest
        {
            Title = "Backend Engineer",
            DepartmentId = "not-a-guid",
            Location = "HCMC",
            Description = "Build APIs",
            Requirements = ["C#"],
            VacancyCount = 1,
            SalaryMin = 3000,
            SalaryMax = 2000
        };

        validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void PatchJobRequestValidator_RejectsEmptyPatch()
    {
        var validator = new PatchJobRequestValidator();

        validator.Validate(new PatchJobRequest()).IsValid.Should().BeFalse();
    }

    [Fact]
    public void PatchJobRequestValidator_AcceptsSingleChangedField()
    {
        var validator = new PatchJobRequestValidator();

        validator.Validate(new PatchJobRequest { Title = "Updated title" }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateInterviewRequestValidator_AcceptsValidRequest()
    {
        var validator = new CreateInterviewRequestValidator();
        var request = new CreateInterviewRequest
        {
            ApplicationId = Guid.NewGuid().ToString(),
            CandidateId = Guid.NewGuid().ToString(),
            JobId = Guid.NewGuid().ToString(),
            Date = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            StartMinutes = 540,
            DurationMinutes = 60,
            Mode = "video",
            LocationOrLink = "https://meet.example.com/abc",
            InterviewerId = Guid.NewGuid().ToString()
        };

        validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateInterviewRequestValidator_RejectsInvalidFields()
    {
        var validator = new CreateInterviewRequestValidator();
        var request = new CreateInterviewRequest
        {
            ApplicationId = "bad",
            CandidateId = string.Empty,
            JobId = "bad",
            StartMinutes = 2000,
            DurationMinutes = 10,
            Mode = string.Empty,
            LocationOrLink = string.Empty,
            InterviewerId = "bad"
        };

        validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateInterviewStatusRequestValidator_RequiresStatus()
    {
        var validator = new UpdateInterviewStatusRequestValidator();

        validator.Validate(new UpdateInterviewStatusRequest()).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateApplicationDecisionRequestValidator_OnlyAllowsConfiguredStatuses()
    {
        var validator = new UpdateApplicationDecisionRequestValidator();

        validator.Validate(new UpdateApplicationDecisionRequest { TargetStatus = "Offer" }).IsValid.Should().BeTrue();
        validator.Validate(new UpdateApplicationDecisionRequest { TargetStatus = "Hired" }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateCandidateSkillsRequestValidator_RequiresAtLeastOnePayload()
    {
        var validator = new UpdateCandidateSkillsRequestValidator();

        validator.Validate(new UpdateCandidateSkillsRequest { SkillIds = null!, Skills = null! }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateCandidateSkillsRequestValidator_RejectsNegativeExperience()
    {
        var validator = new UpdateCandidateSkillsRequestValidator();
        var request = new UpdateCandidateSkillsRequest
        {
            Skills =
            [
                new CandidateSkillUpsertRequest
                {
                    SkillId = "skill-1",
                    YearsOfExperience = -1
                }
            ]
        };

        validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateCandidateProfileRequestValidator_AcceptsValidProfile()
    {
        var validator = new UpdateCandidateProfileRequestValidator();
        var request = new UpdateCandidateProfileRequest
        {
            Name = "Nguyen Van A",
            Email = "a@example.com",
            Phone = "0123456789",
            Skills =
            [
                new CandidateSkillUpsertRequest
                {
                    SkillId = "skill-1",
                    YearsOfExperience = 2
                }
            ]
        };

        validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateCandidateProfileRequestValidator_RejectsMissingNameAndBadEmail()
    {
        var validator = new UpdateCandidateProfileRequestValidator();
        var request = new UpdateCandidateProfileRequest
        {
            Name = "",
            Email = "not-an-email"
        };

        validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpsertCandidateExperienceRequestValidator_AcceptsCurrentRoleWithoutEndDate()
    {
        var validator = new UpsertCandidateExperienceRequestValidator();
        var request = new UpsertCandidateExperienceRequest
        {
            Title = "Software Engineer",
            Company = "RecruitPro",
            Bullets = ["Built APIs"],
            Period = new CandidateExperiencePeriodRequest
            {
                StartMonth = 1,
                StartYear = 2024,
                IsCurrent = true
            }
        };

        validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpsertCandidateExperienceRequestValidator_RejectsInvalidPeriod()
    {
        var validator = new UpsertCandidateExperienceRequestValidator();
        var request = new UpsertCandidateExperienceRequest
        {
            Title = "Software Engineer",
            Company = "RecruitPro",
            Bullets = ["Built APIs"],
            Period = new CandidateExperiencePeriodRequest
            {
                StartMonth = 13,
                StartYear = 1800,
                EndMonth = 15,
                EndYear = 1800,
                IsCurrent = false
            }
        };

        validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SendApplicationEmailRequestValidator_RequiresTemplateType()
    {
        var validator = new SendApplicationEmailRequestValidator();

        validator.Validate(new SendApplicationEmailRequest()).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SendApplicationEmailRequestValidator_AcceptsValidPayload()
    {
        var validator = new SendApplicationEmailRequestValidator();
        var request = new SendApplicationEmailRequest
        {
            TemplateType = "Offer",
            Subject = "Interview invitation"
        };

        validator.Validate(request).IsValid.Should().BeTrue();
    }
}
