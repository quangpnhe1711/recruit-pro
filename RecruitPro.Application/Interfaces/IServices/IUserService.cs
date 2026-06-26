using RecruitPro.Application.DTOs.Response;

namespace RecruitPro.Application.Interfaces.IServices;

public interface IUserService
{
    /// <summary>
    /// Users who can be assigned as recruitment owners: recruiters (HR role) and department heads
    /// (HeadDepartment role). Candidates are never included.
    /// </summary>
    Task<ApiResponse<AssignableRecruitmentOwnersDto>> GetAssignableRecruitmentOwnersAsync();
}
