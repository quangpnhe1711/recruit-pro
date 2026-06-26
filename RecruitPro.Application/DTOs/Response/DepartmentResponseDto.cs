namespace RecruitPro.Application.DTOs.Response;

/// <summary>
/// Department with its head (Phase 2/3). Returned by the department lookup, detail, and update
/// endpoints. <c>HeadUser*</c> are null when the department has no head assigned.
/// </summary>
public class DepartmentResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? HeadUserId { get; set; }
    public string? HeadUserName { get; set; }
    public string? HeadUserEmail { get; set; }
}
