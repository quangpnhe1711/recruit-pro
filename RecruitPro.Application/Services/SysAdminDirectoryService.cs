using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Request.SysAdmin;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.SysAdmin;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Application.Services;

public class SysAdminDirectoryService : ISysAdminDirectoryService
{
    private static readonly string[] AllowedStatuses =
    [
        UserStatus.Active.ToString(),
        UserStatus.Inactive.ToString(),
        UserStatus.Blocked.ToString(),
    ];

    private readonly IRbacRepository _rbacRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SysAdminDirectoryService> _logger;

    public SysAdminDirectoryService(IRbacRepository rbacRepository, IUnitOfWork unitOfWork, ILogger<SysAdminDirectoryService> logger)
    {
        _rbacRepository = rbacRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<PaginatedResponseDto<SysAdminUserDto>>> QueryUsersAsync(
        string? search, string? roleId, string? status, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        Guid? roleGuid = Guid.TryParse(roleId, out Guid parsedRole) ? parsedRole : null;

        var (items, total) = await _rbacRepository.QueryUsersAsync(search, roleGuid, status, page, pageSize);
        return ApiResponse<PaginatedResponseDto<SysAdminUserDto>>.Ok(new PaginatedResponseDto<SysAdminUserDto>
        {
            Items = items.Select(ToUserDto).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = total,
        });
    }

    public async Task<ApiResponse<SysAdminUserDto>> UpdateUserStatusAsync(
        string userId, UpdateUserStatusRequest request, Guid currentUserId)
    {
        if (!Guid.TryParse(userId, out Guid userGuid))
        {
            return ApiResponse<SysAdminUserDto>.NotFound(ErrorCodes.UserNotFound);
        }

        string? newStatus = AllowedStatuses.FirstOrDefault(
            allowed => string.Equals(allowed, request.Status?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (newStatus == null)
        {
            return ApiResponse<SysAdminUserDto>.BadRequest(ErrorCodes.UserStatusInvalid);
        }

        User? user = await _rbacRepository.GetUserWithRolesAsync(userGuid);
        if (user == null)
        {
            return ApiResponse<SysAdminUserDto>.NotFound(ErrorCodes.UserNotFound);
        }

        bool deactivating = !string.Equals(newStatus, UserStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase);

        // 409 — admins cannot deactivate their own account (they would cut off their session mid-flight).
        if (deactivating && user.Id == currentUserId)
        {
            return ApiResponse<SysAdminUserDto>.Conflict(ErrorCodes.UserSelfDeactivation);
        }

        // 409 — last-administrator protection: the system must always keep at least one ACTIVE user
        // holding PERMISSION_MANAGE.
        if (deactivating && await _rbacRepository.UserHasPermissionAsync(user.Id, RbacCatalog.ManagePermissionsCode))
        {
            int otherAdmins = await _rbacRepository.CountOtherActiveUsersWithPermissionAsync(
                RbacCatalog.ManagePermissionsCode, user.Id);
            if (otherAdmins == 0)
            {
                return ApiResponse<SysAdminUserDto>.Conflict(ErrorCodes.RbacAdminLockout);
            }
        }

        string previousStatus = user.Status ?? UserStatus.Active.ToString();
        user.Status = newStatus;
        user.UpdatedAt = DbDateTime.Now;
        await _rbacRepository.AddSystemLogAsync(new SystemLog
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            Action = "USER_STATUS_CHANGED",
            Description = $"Đổi trạng thái tài khoản {user.Email}: {previousStatus} → {newStatus}.",
            CreatedAt = DbDateTime.Now,
        });
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("User {UserId} status changed {Old} -> {New} by {AdminId}.",
            user.Id, previousStatus, newStatus, currentUserId);

        return ApiResponse<SysAdminUserDto>.Ok(ToUserDto(user), "Đã cập nhật trạng thái tài khoản.");
    }

    public async Task<ApiResponse<SysAdminUserDto>> UpdateUserRolesAsync(
        string userId, UpdateUserRolesRequest request, Guid currentUserId)
    {
        if (!Guid.TryParse(userId, out Guid userGuid))
        {
            return ApiResponse<SysAdminUserDto>.NotFound(ErrorCodes.UserNotFound);
        }

        List<Guid> roleGuids = new();
        foreach (string rawRoleId in request.RoleIds ?? new List<string>())
        {
            if (!Guid.TryParse(rawRoleId, out Guid parsed))
            {
                return ApiResponse<SysAdminUserDto>.BadRequest(ErrorCodes.RbacRoleNotFound);
            }

            roleGuids.Add(parsed);
        }

        roleGuids = roleGuids.Distinct().ToList();
        if (roleGuids.Count == 0)
        {
            return ApiResponse<SysAdminUserDto>.BadRequest(ErrorCodes.RbacRoleNotFound);
        }

        User? user = await _rbacRepository.GetUserWithRolesAsync(userGuid);
        if (user == null)
        {
            return ApiResponse<SysAdminUserDto>.NotFound(ErrorCodes.UserNotFound);
        }

        List<Role> roles = await _rbacRepository.GetRolesByIdsAsync(roleGuids);
        if (roles.Count != roleGuids.Count)
        {
            return ApiResponse<SysAdminUserDto>.BadRequest(ErrorCodes.RbacRoleNotFound);
        }

        // 409 — last-administrator protection: if this user is currently an active RBAC administrator
        // and the new role set no longer grants PERMISSION_MANAGE, someone else must still hold it.
        bool userIsActive = string.Equals(user.Status ?? UserStatus.Active.ToString(),
            UserStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase);
        if (userIsActive && await _rbacRepository.UserHasPermissionAsync(user.Id, RbacCatalog.ManagePermissionsCode))
        {
            bool newRolesGrantManage = roles.Any(role => role.RolePermissions.Any(rp =>
                string.Equals(rp.Permission.Code, RbacCatalog.ManagePermissionsCode, StringComparison.OrdinalIgnoreCase)));
            if (!newRolesGrantManage)
            {
                int otherAdmins = await _rbacRepository.CountOtherActiveUsersWithPermissionAsync(
                    RbacCatalog.ManagePermissionsCode, user.Id);
                if (otherAdmins == 0)
                {
                    return ApiResponse<SysAdminUserDto>.Conflict(ErrorCodes.RbacAdminLockout);
                }
            }
        }

        await _rbacRepository.ReplaceUserRolesAsync(user, roles);
        await _rbacRepository.AddSystemLogAsync(new SystemLog
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            Action = "USER_ROLES_CHANGED",
            Description = $"Cập nhật vai trò cho {user.Email}: {string.Join(", ", roles.Select(r => r.Name))}.",
            CreatedAt = DbDateTime.Now,
        });
        await _unitOfWork.SaveChangesAsync();

        User refreshed = (await _rbacRepository.GetUserWithRolesAsync(userGuid))!;
        return ApiResponse<SysAdminUserDto>.Ok(ToUserDto(refreshed), "Đã cập nhật vai trò.");
    }

    public async Task<ApiResponse<PaginatedResponseDto<SystemLogDto>>> QuerySystemLogsAsync(
        string? search, string? userId, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        Guid? userGuid = Guid.TryParse(userId, out Guid parsedUser) ? parsedUser : null;

        var (items, total) = await _rbacRepository.QuerySystemLogsAsync(search, userGuid, page, pageSize);
        return ApiResponse<PaginatedResponseDto<SystemLogDto>>.Ok(new PaginatedResponseDto<SystemLogDto>
        {
            Items = items.Select(ToLogDto).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = total,
        });
    }

    public async Task<ApiResponse<SysAdminOverviewDto>> GetOverviewAsync()
    {
        DateTime now = DbDateTime.Now;
        SysAdminOverviewCounts counts = await _rbacRepository.GetOverviewCountsAsync(now);
        var (recentLogs, _) = await _rbacRepository.QuerySystemLogsAsync(search: null, userId: null, page: 1, pageSize: 8);

        List<Role> roles = await _rbacRepository.GetRolesWithPermissionsAsync();
        Dictionary<Guid, int> userCounts = await _rbacRepository.CountUsersByRoleAsync();

        return ApiResponse<SysAdminOverviewDto>.Ok(new SysAdminOverviewDto
        {
            TotalUsers = counts.TotalUsers,
            ActiveUsers = counts.ActiveUsers,
            InactiveUsers = counts.TotalUsers - counts.ActiveUsers,
            TotalRoles = counts.TotalRoles,
            TotalPermissions = counts.TotalPermissions,
            OpenJobs = counts.OpenJobs,
            JobsClosingSoon = counts.JobsClosingSoon,
            PendingApprovalJobs = counts.PendingApprovalJobs,
            TotalApplications = counts.TotalApplications,
            ApplicationsLast7Days = counts.ApplicationsLast7Days,
            UpcomingInterviews = counts.UpcomingInterviews,
            EnabledWorkflows = counts.EnabledWorkflows,
            ExecutionsLast7Days = counts.ExecutionsLast7Days,
            FailedExecutionsLast7Days = counts.FailedExecutionsLast7Days,
            RecentLogs = recentLogs.Select(ToLogDto).ToList(),
            Roles = roles
                .OrderBy(role => role.Name)
                .Select(role => new RbacRoleDto
                {
                    Id = role.Id.ToString(),
                    Name = role.Name,
                    Description = role.Description,
                    UserCount = userCounts.TryGetValue(role.Id, out int count) ? count : 0,
                    PermissionCount = role.RolePermissions.Count,
                    IsSystemAdmin = string.Equals(role.Name, Domain.Constants.RoleNames.SystemAdmin, StringComparison.OrdinalIgnoreCase),
                })
                .ToList(),
        });
    }

    private static SysAdminUserDto ToUserDto(User user)
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

    private static SystemLogDto ToLogDto(SystemLog log)
    {
        return new SystemLogDto
        {
            Id = log.Id.ToString(),
            Action = log.Action,
            Description = log.Description,
            CreatedAt = log.CreatedAt,
            UserId = log.UserId?.ToString(),
            UserFullName = log.User?.FullName,
            UserEmail = log.User?.Email,
        };
    }
}
