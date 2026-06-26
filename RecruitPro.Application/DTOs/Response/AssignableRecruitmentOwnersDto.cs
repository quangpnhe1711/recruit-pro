namespace RecruitPro.Application.DTOs.Response;

/// <summary>
/// A user that can be assigned as a recruitment owner (recruiter or department head). Candidates are
/// never returned in these lists.
/// </summary>
public class RecruitmentOwnerDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Assignable recruitment owners, grouped by role. <c>recruiters</c> = active HR-role users;
/// <c>departmentHeads</c> = active HeadDepartment-role users.
/// </summary>
public class AssignableRecruitmentOwnersDto
{
    public List<RecruitmentOwnerDto> Recruiters { get; set; } = [];
    public List<RecruitmentOwnerDto> DepartmentHeads { get; set; } = [];
}
