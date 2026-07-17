using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Request.Users;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.SysAdmin;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Services;

public sealed class UserService : IUserService
{
    private const int MaxFullNameLength = 200;
    private const int MaxPhoneLength = 30;

    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<SysAdminUserDto>> GetProfileAsync(Guid userId)
    {
        User? user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return ApiResponse<SysAdminUserDto>.NotFound(ErrorCodes.UserNotFound);
        }

        return ApiResponse<SysAdminUserDto>.Ok(MapProfile(user));
    }

    public async Task<ApiResponse<SysAdminUserDto>> UpdateProfileAsync(Guid userId, UpdateInternalProfileRequest request)
    {
        string fullName = (request.FullName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return ApiResponse<SysAdminUserDto>.BadRequest(ErrorCodes.Required);
        }

        if (fullName.Length > MaxFullNameLength)
        {
            return ApiResponse<SysAdminUserDto>.BadRequest(ErrorCodes.MaxLengthExceeded);
        }

        string? phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        if (phone != null && phone.Length > MaxPhoneLength)
        {
            return ApiResponse<SysAdminUserDto>.BadRequest(ErrorCodes.MaxLengthExceeded);
        }

        User? user = await _userRepository.GetTrackedByIdAsync(userId);
        if (user == null)
        {
            return ApiResponse<SysAdminUserDto>.NotFound(ErrorCodes.UserNotFound);
        }

        user.FullName = fullName;
        user.Phone = phone;
        user.UpdatedAt = DbDateTime.Now;

        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<SysAdminUserDto>.Ok(MapProfile(user));
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

    // Mirrors SysAdminDirectoryService.ToUserDto — account info + read-only role refs.
    private static SysAdminUserDto MapProfile(User user)
    {
        return new SysAdminUserDto
        {
            Id = user.Id.ToString(),
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status ?? UserStatus.Active.ToString(),
            CreatedAt = user.CreatedAt,
            Roles = user.UserRoles
                .Select(userRole => new RbacRoleRefDto
                {
                    Id = userRole.RoleId.ToString(),
                    Name = userRole.Role?.Name ?? string.Empty,
                })
                .OrderBy(role => role.Name)
                .ToList(),
        };
    }
}
