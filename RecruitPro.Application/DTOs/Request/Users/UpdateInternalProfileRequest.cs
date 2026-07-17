namespace RecruitPro.Application.DTOs.Request.Users;

// Only the fields an internal user may edit on their own profile. Username, email, roles, department and
// status are intentionally NOT accepted here — those are administered via the SysAdmin directory.
public class UpdateInternalProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
}
