namespace RecruitPro.Application.DTOs.Response;

public class HrJobListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string CreatedDate { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public int ApplicationsCount { get; set; }
    public HrJobCreatorDto CreatedBy { get; set; } = new();

    // Phase 2/3 ownership (list view). EffectiveDepartmentHead = Department.HeadUser ?? ApprovedBy user.
    public string? RecruiterId { get; set; }
    public string? RecruiterName { get; set; }
    public string? DepartmentHeadId { get; set; }
    public string? DepartmentHeadName { get; set; }
    public string? EffectiveDepartmentHeadId { get; set; }
    public string? EffectiveDepartmentHeadName { get; set; }
}

public class HrJobCreatorDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
