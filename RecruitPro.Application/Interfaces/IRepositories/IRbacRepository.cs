using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IRepositories;

/// <summary>
/// Data access for the System Admin console: roles/permissions matrix, user directory, and audit logs.
/// </summary>
public interface IRbacRepository
{
    Task<List<Role>> GetRolesWithPermissionsAsync();

    Task<Role?> GetRoleWithPermissionsAsync(Guid roleId);

    Task<Dictionary<Guid, int>> CountUsersByRoleAsync();

    Task<List<Permission>> GetPermissionsByCodesAsync(IReadOnlyCollection<string> codes);

    Task ReplaceRolePermissionsAsync(Role role, IReadOnlyCollection<Permission> permissions);

    /// <summary>
    /// Active users who would still hold <paramref name="permissionCode"/> if it were removed from
    /// <paramref name="excludedRoleId"/>. Guards the RBAC-lockout rule (last-administrator protection).
    /// </summary>
    Task<int> CountActiveUsersWithPermissionExcludingRoleAsync(string permissionCode, Guid excludedRoleId);

    /// <summary>Active users other than <paramref name="excludedUserId"/> who hold <paramref name="permissionCode"/>.</summary>
    Task<int> CountOtherActiveUsersWithPermissionAsync(string permissionCode, Guid excludedUserId);

    Task<bool> UserHasPermissionAsync(Guid userId, string permissionCode);

    Task<(List<User> Items, int Total)> QueryUsersAsync(string? search, Guid? roleId, string? status, int page, int pageSize);

    Task<User?> GetUserWithRolesAsync(Guid userId);

    Task<List<Role>> GetRolesByIdsAsync(IReadOnlyCollection<Guid> roleIds);

    Task ReplaceUserRolesAsync(User user, IReadOnlyCollection<Role> roles);

    Task<(List<SystemLog> Items, int Total)> QuerySystemLogsAsync(string? search, Guid? userId, int page, int pageSize);

    Task AddSystemLogAsync(SystemLog log);

    Task<SysAdminOverviewCounts> GetOverviewCountsAsync(DateTime now);
}

/// <summary>Raw aggregate counts for the System Admin overview dashboard.</summary>
public sealed class SysAdminOverviewCounts
{
    public int TotalUsers { get; init; }
    public int ActiveUsers { get; init; }
    public int TotalRoles { get; init; }
    public int TotalPermissions { get; init; }
    public int OpenJobs { get; init; }
    public int JobsClosingSoon { get; init; }
    public int PendingApprovalJobs { get; init; }
    public int TotalApplications { get; init; }
    public int ApplicationsLast7Days { get; init; }
    public int UpcomingInterviews { get; init; }
    public int EnabledWorkflows { get; init; }
    public int ExecutionsLast7Days { get; init; }
    public int FailedExecutionsLast7Days { get; init; }
}
