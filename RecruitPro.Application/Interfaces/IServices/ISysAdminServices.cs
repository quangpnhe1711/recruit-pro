using System;
using System.Threading.Tasks;
using RecruitPro.Application.DTOs.Request.SysAdmin;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.SysAdmin;

namespace RecruitPro.Application.Interfaces.IServices;

/// <summary>
/// Live permission check against the RBAC tables (user_roles → role_permissions → permissions.code).
/// This is the security boundary behind <c>[RequirePermission]</c>: changes saved in the matrix apply
/// on the next request, without waiting for a new JWT.
/// </summary>
public interface IPermissionCheckService
{
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode);
}

public interface ISysAdminRbacService
{
    Task<ApiResponse<System.Collections.Generic.List<RbacRoleDto>>> GetRolesAsync();

    ApiResponse<System.Collections.Generic.List<RbacModuleDto>> GetModules();

    Task<ApiResponse<RolePermissionsDto>> GetRolePermissionsAsync(string roleId);

    Task<ApiResponse<RolePermissionsDto>> UpdateRolePermissionsAsync(string roleId, UpdateRolePermissionsRequest request, Guid currentUserId);
}

public interface ISysAdminDirectoryService
{
    Task<ApiResponse<PaginatedResponseDto<SysAdminUserDto>>> QueryUsersAsync(string? search, string? roleId, string? status, int page, int pageSize);

    Task<ApiResponse<SysAdminUserDto>> UpdateUserStatusAsync(string userId, UpdateUserStatusRequest request, Guid currentUserId);

    Task<ApiResponse<SysAdminUserDto>> UpdateUserRolesAsync(string userId, UpdateUserRolesRequest request, Guid currentUserId);

    Task<ApiResponse<PaginatedResponseDto<SystemLogDto>>> QuerySystemLogsAsync(string? search, string? userId, int page, int pageSize);

    Task<ApiResponse<SysAdminOverviewDto>> GetOverviewAsync();
}
