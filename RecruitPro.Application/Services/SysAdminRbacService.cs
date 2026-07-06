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
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Services;

public class SysAdminRbacService : ISysAdminRbacService
{
    private readonly IRbacRepository _rbacRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SysAdminRbacService> _logger;

    public SysAdminRbacService(IRbacRepository rbacRepository, IUnitOfWork unitOfWork, ILogger<SysAdminRbacService> logger)
    {
        _rbacRepository = rbacRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<List<RbacRoleDto>>> GetRolesAsync()
    {
        List<Role> roles = await _rbacRepository.GetRolesWithPermissionsAsync();
        Dictionary<Guid, int> userCounts = await _rbacRepository.CountUsersByRoleAsync();

        List<RbacRoleDto> result = roles
            .OrderBy(role => role.Name)
            .Select(role => ToRoleDto(role, userCounts))
            .ToList();

        return ApiResponse<List<RbacRoleDto>>.Ok(result);
    }

    public ApiResponse<List<RbacModuleDto>> GetModules()
    {
        List<RbacModuleDto> modules = RbacCatalog.Modules
            .Select(module => new RbacModuleDto
            {
                Key = module.Key,
                Group = module.Group,
                Actions = module.Actions.Select(action => new RbacActionDto
                {
                    Code = action.Code,
                    Action = action.Action,
                    Name = action.Code.Replace('_', ' '),
                    IsCritical = action.IsCritical,
                }).ToList(),
            })
            .ToList();

        return ApiResponse<List<RbacModuleDto>>.Ok(modules);
    }

    public async Task<ApiResponse<RolePermissionsDto>> GetRolePermissionsAsync(string roleId)
    {
        if (!Guid.TryParse(roleId, out Guid roleGuid))
        {
            return ApiResponse<RolePermissionsDto>.NotFound("Không tìm thấy vai trò.", ErrorCodes.RbacRoleNotFound);
        }

        Role? role = await _rbacRepository.GetRoleWithPermissionsAsync(roleGuid);
        if (role == null)
        {
            return ApiResponse<RolePermissionsDto>.NotFound("Không tìm thấy vai trò.", ErrorCodes.RbacRoleNotFound);
        }

        return ApiResponse<RolePermissionsDto>.Ok(ToRolePermissionsDto(role));
    }

    public async Task<ApiResponse<RolePermissionsDto>> UpdateRolePermissionsAsync(
        string roleId, UpdateRolePermissionsRequest request, Guid currentUserId)
    {
        if (!Guid.TryParse(roleId, out Guid roleGuid))
        {
            return ApiResponse<RolePermissionsDto>.NotFound("Không tìm thấy vai trò.", ErrorCodes.RbacRoleNotFound);
        }

        // 400 — every submitted code must exist in the catalog (backend source of truth).
        List<string> canonicalCodes = new();
        List<string> unknownCodes = new();
        foreach (string submitted in request.PermissionCodes ?? new List<string>())
        {
            if (RbacCatalog.TryNormalizeCode(submitted, out string canonical))
            {
                canonicalCodes.Add(canonical);
            }
            else
            {
                unknownCodes.Add(submitted);
            }
        }

        if (unknownCodes.Count > 0)
        {
            return ApiResponse<RolePermissionsDto>.BadRequest(
                $"Quyền không tồn tại: {string.Join(", ", unknownCodes)}.", ErrorCodes.RbacUnknownPermission);
        }

        canonicalCodes = canonicalCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        Role? role = await _rbacRepository.GetRoleWithPermissionsAsync(roleGuid);
        if (role == null)
        {
            return ApiResponse<RolePermissionsDto>.NotFound("Không tìm thấy vai trò.", ErrorCodes.RbacRoleNotFound);
        }

        // 409 — last-administrator protection: removing PERMISSION_MANAGE from this role must not leave
        // the system with zero active users able to manage RBAC (including the current admin locking
        // themselves out). Restoring access would otherwise require direct DB surgery.
        bool roleCurrentlyGrantsManage = role.RolePermissions
            .Any(rp => string.Equals(rp.Permission.Code, RbacCatalog.ManagePermissionsCode, StringComparison.OrdinalIgnoreCase));
        bool newSetGrantsManage = canonicalCodes
            .Contains(RbacCatalog.ManagePermissionsCode, StringComparer.OrdinalIgnoreCase);

        if (roleCurrentlyGrantsManage && !newSetGrantsManage)
        {
            int remainingAdmins = await _rbacRepository.CountActiveUsersWithPermissionExcludingRoleAsync(
                RbacCatalog.ManagePermissionsCode, roleGuid);
            if (remainingAdmins == 0)
            {
                return ApiResponse<RolePermissionsDto>.Conflict(
                    "Không thể gỡ quyền quản trị RBAC khỏi vai trò này: sẽ không còn quản trị viên nào có thể quản lý phân quyền.",
                    errorCode: ErrorCodes.RbacAdminLockout);
            }
        }

        List<Permission> permissions = await _rbacRepository.GetPermissionsByCodesAsync(canonicalCodes);
        if (permissions.Count != canonicalCodes.Count)
        {
            // Catalog and DB seed drifted apart — surface which codes have no permission row.
            HashSet<string> found = permissions.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            List<string> missing = canonicalCodes.Where(code => !found.Contains(code)).ToList();
            return ApiResponse<RolePermissionsDto>.BadRequest(
                $"Quyền chưa được khởi tạo trong hệ thống: {string.Join(", ", missing)}.", ErrorCodes.RbacUnknownPermission);
        }

        await _rbacRepository.ReplaceRolePermissionsAsync(role, permissions);
        await _rbacRepository.AddSystemLogAsync(new SystemLog
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            Action = "RBAC_PERMISSIONS_UPDATED",
            Description = $"Cập nhật quyền cho vai trò {role.Name}: {permissions.Count} quyền được cấp.",
            CreatedAt = DbDateTime.Now,
        });
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("RBAC updated: role {RoleName} now has {Count} permissions (by {UserId}).",
            role.Name, permissions.Count, currentUserId);

        Role refreshed = (await _rbacRepository.GetRoleWithPermissionsAsync(roleGuid))!;
        return ApiResponse<RolePermissionsDto>.Ok(ToRolePermissionsDto(refreshed), "Đã lưu phân quyền.");
    }

    private static RbacRoleDto ToRoleDto(Role role, Dictionary<Guid, int> userCounts)
    {
        return new RbacRoleDto
        {
            Id = role.Id.ToString(),
            Name = role.Name,
            Description = role.Description,
            UserCount = userCounts.TryGetValue(role.Id, out int count) ? count : 0,
            PermissionCount = role.RolePermissions.Count,
            IsSystemAdmin = string.Equals(role.Name, RoleNames.SystemAdmin, StringComparison.OrdinalIgnoreCase),
        };
    }

    private static RolePermissionsDto ToRolePermissionsDto(Role role)
    {
        return new RolePermissionsDto
        {
            RoleId = role.Id.ToString(),
            RoleName = role.Name,
            RoleDescription = role.Description,
            GrantedCodes = role.RolePermissions
                .Select(rp => rp.Permission.Code)
                .OrderBy(code => code)
                .ToList(),
        };
    }
}
