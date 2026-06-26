using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<ApiResponse<AssignableRecruitmentOwnersDto>> GetAssignableRecruitmentOwnersAsync()
    {
        // Reuse the existing role-membership query. Candidates (Candidate role only) are excluded by
        // construction — they are never in the HR / HeadDepartment result sets.
        IReadOnlyList<User> recruiters = await _userRepository.GetUsersInRolesAsync(RoleNames.Hr);
        IReadOnlyList<User> departmentHeads = await _userRepository.GetUsersInRolesAsync(RoleNames.HeadDepartment);

        return ApiResponse<AssignableRecruitmentOwnersDto>.Ok(new AssignableRecruitmentOwnersDto
        {
            Recruiters = recruiters.Select(MapOwner).ToList(),
            DepartmentHeads = departmentHeads.Select(MapOwner).ToList()
        });
    }

    private static RecruitmentOwnerDto MapOwner(User user)
    {
        return new RecruitmentOwnerDto
        {
            Id = user.Id.ToString(),
            FullName = user.FullName,
            Email = user.Email
        };
    }
}
