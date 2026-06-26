namespace RecruitPro.Application.DTOs.Request.Departments;

/// <summary>
/// Update a department's editable fields. <c>HeadUserId</c> sets the department head
/// (must be an existing user with the HeadDepartment or SystemAdmin role); pass null to leave it
/// unchanged. <c>Name</c>/<c>Description</c> are optional edits.
/// </summary>
public class UpdateDepartmentRequest
{
    public string? HeadUserId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}
