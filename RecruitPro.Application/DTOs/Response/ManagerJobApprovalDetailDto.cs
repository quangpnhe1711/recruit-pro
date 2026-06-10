namespace RecruitPro.Application.DTOs.Response;

public class ManagerJobApprovalDetailDto
{
    public string JobId { get; set; } = string.Empty;
    public string ReferenceCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string SubmittedAgoLabel { get; set; } = string.Empty;
    public ManagerJobApprovalUserDto HrOwner { get; set; } = new();
    public ManagerJobApprovalDepartmentDto Department { get; set; } = new();
    public string Location { get; set; } = string.Empty;
    public string WorkMode { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public int VacancyCount { get; set; }
    public int? MinExperienceYears { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public DateTime? Deadline { get; set; }
    public List<string> Description { get; set; } = [];
    public List<string> Requirements { get; set; } = [];
    public List<string> Benefits { get; set; } = [];
    public List<ManagerJobApprovalSkillDto> Skills { get; set; } = [];
    public ManagerJobApprovalInsightDto Insights { get; set; } = new();
    public List<ManagerJobApprovalStepDto> InterviewFlow { get; set; } = [];
    public ManagerJobApprovalHistoryDto? ApprovalSnapshot { get; set; }
}

public class ManagerJobApprovalUserDto
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public class ManagerJobApprovalDepartmentDto
{
    public string DepartmentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class ManagerJobApprovalSkillDto
{
    public string SkillId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? MinYearsExperience { get; set; }
    public bool IsRequired { get; set; }
}

public class ManagerJobApprovalInsightDto
{
    public int ApplicationsCount { get; set; }
    public int ActivePipelineCount { get; set; }
    public int RequiredSkillsCount { get; set; }
    public int OptionalSkillsCount { get; set; }
    public bool HasSalaryRange { get; set; }
}

public class ManagerJobApprovalStepDto
{
    public int Order { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ManagerJobApprovalHistoryDto
{
    public string? ApprovedByName { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string Summary { get; set; } = string.Empty;
}
