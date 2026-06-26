namespace RecruitPro.Application.DTOs.Response;

public class ApplyJobScreenDto
{
    public ApplyJobJobSummaryDto Job { get; set; } = new();
    public ApplyJobCandidateProfileDto CandidateProfile { get; set; } = new();
    public ApplyJobResumeDto? Resume { get; set; }
    public ApplyJobEligibilityDto Eligibility { get; set; } = new();
}

public class ApplyJobJobSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string WorkMode { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string SalaryLabel { get; set; } = string.Empty;
    public int VacancyCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }
}

public class ApplyJobCandidateProfileDto
{
    public string CandidateId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CurrentPosition { get; set; }
    public int? ExperienceYears { get; set; }
    public string EditProfilePath { get; set; } = "/candidate/profile";
}

public class ApplyJobResumeDto
{
    public string ResumeId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

public class ApplyJobEligibilityDto
{
    public bool CanApply { get; set; }
    public bool AlreadyApplied { get; set; }
    public string? ExistingApplicationId { get; set; }
    public string? ExistingApplicationStatus { get; set; }
    public List<string> Blockers { get; set; } = [];
    public string GuidanceMessage { get; set; } = string.Empty;

    /// <summary>
    /// Stable machine error code for the primary blocker (null when CanApply). The frontend should
    /// branch on this before HTTP status or localized message (INV-012).
    /// </summary>
    public string? PrimaryErrorCode { get; set; }
}
