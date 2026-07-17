using RecruitPro.Application.DTOs.Request.Users;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.SysAdmin;

namespace RecruitPro.Application.Interfaces.IServices;

public interface IUserService
{
    /// <summary>
    /// Users who can be assigned as recruitment owners: recruiters (HR role) and department heads
    /// (HeadDepartment role). Candidates are never included.
    /// </summary>
    Task<ApiResponse<AssignableRecruitmentOwnersDto>> GetAssignableRecruitmentOwnersAsync();

    /// <summary>Reads the signed-in internal user's own profile (account info + roles, read-only).</summary>
    Task<ApiResponse<SysAdminUserDto>> GetProfileAsync(Guid userId);

    /// <summary>Updates the signed-in internal user's editable profile fields (full name, phone).</summary>
    Task<ApiResponse<SysAdminUserDto>> UpdateProfileAsync(Guid userId, UpdateInternalProfileRequest request);
}
