using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Automation;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;

namespace RecruitPro.Infrastructure.Repositories;

public class RbacRepository : IRbacRepository
{
    private static readonly string ActiveStatus = UserStatus.Active.ToString();

    private readonly AppDbContext _context;

    public RbacRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<Role>> GetRolesWithPermissionsAsync()
    {
        return _context.Roles
            .Include(role => role.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .AsSplitQuery()
            .ToListAsync();
    }

    public Task<Role?> GetRoleWithPermissionsAsync(Guid roleId)
    {
        return _context.Roles
            .Include(role => role.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .AsSplitQuery()
            .FirstOrDefaultAsync(role => role.Id == roleId);
    }

    public async Task<Dictionary<Guid, int>> CountUsersByRoleAsync()
    {
        var counts = await _context.UserRoles
            .GroupBy(userRole => userRole.RoleId)
            .Select(group => new { RoleId = group.Key, Count = group.Count() })
            .ToListAsync();

        return counts.ToDictionary(entry => entry.RoleId, entry => entry.Count);
    }

    public Task<List<Permission>> GetPermissionsByCodesAsync(IReadOnlyCollection<string> codes)
    {
        List<string> lowered = codes.Select(code => code.ToLower()).ToList();
        return _context.Permissions
            .Where(permission => lowered.Contains(permission.Code.ToLower()))
            .ToListAsync();
    }

    public Task ReplaceRolePermissionsAsync(Role role, IReadOnlyCollection<Permission> permissions)
    {
        // Diff instead of clear+re-add: re-adding a row with the same composite key in one SaveChanges
        // would make EF track a delete and an insert of the same key.
        HashSet<Guid> targetIds = permissions.Select(permission => permission.Id).ToHashSet();
        List<RolePermission> revoked = role.RolePermissions
            .Where(rolePermission => !targetIds.Contains(rolePermission.PermissionId))
            .ToList();
        foreach (RolePermission rolePermission in revoked)
        {
            role.RolePermissions.Remove(rolePermission);
            _context.RolePermissions.Remove(rolePermission);
        }

        HashSet<Guid> existingIds = role.RolePermissions.Select(rolePermission => rolePermission.PermissionId).ToHashSet();
        foreach (Permission permission in permissions.Where(permission => !existingIds.Contains(permission.Id)))
        {
            role.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permission.Id,
                // assigned_at is timestamptz — Npgsql requires Kind=Utc.
                AssignedAt = DateTime.UtcNow,
            });
        }

        return Task.CompletedTask;
    }

    public Task<int> CountActiveUsersWithPermissionExcludingRoleAsync(string permissionCode, Guid excludedRoleId)
    {
        string lowered = permissionCode.ToLower();
        return _context.Users
            .Where(user => user.Status == null || user.Status == ActiveStatus)
            .Where(user => user.UserRoles.Any(userRole =>
                userRole.RoleId != excludedRoleId
                && userRole.Role.RolePermissions.Any(rolePermission => rolePermission.Permission.Code.ToLower() == lowered)))
            .CountAsync();
    }

    public Task<int> CountOtherActiveUsersWithPermissionAsync(string permissionCode, Guid excludedUserId)
    {
        string lowered = permissionCode.ToLower();
        return _context.Users
            .Where(user => user.Id != excludedUserId)
            .Where(user => user.Status == null || user.Status == ActiveStatus)
            .Where(user => user.UserRoles.Any(userRole =>
                userRole.Role.RolePermissions.Any(rolePermission => rolePermission.Permission.Code.ToLower() == lowered)))
            .CountAsync();
    }

    public Task<bool> UserHasPermissionAsync(Guid userId, string permissionCode)
    {
        string lowered = permissionCode.ToLower();
        return _context.Users
            .Where(user => user.Id == userId)
            .Where(user => user.Status == null || user.Status == ActiveStatus)
            .AnyAsync(user => user.UserRoles.Any(userRole =>
                userRole.Role.RolePermissions.Any(rolePermission => rolePermission.Permission.Code.ToLower() == lowered)));
    }

    public async Task<(List<User> Items, int Total)> QueryUsersAsync(
        string? search, Guid? roleId, string? status, int page, int pageSize)
    {
        IQueryable<User> query = _context.Users
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .AsSplitQuery();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = $"%{search.Trim()}%";
            query = query.Where(user =>
                EF.Functions.ILike(user.FullName, term)
                || EF.Functions.ILike(user.Email, term)
                || EF.Functions.ILike(user.Username, term));
        }

        if (roleId.HasValue)
        {
            query = query.Where(user => user.UserRoles.Any(userRole => userRole.RoleId == roleId.Value));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            string loweredStatus = status.Trim().ToLower();
            query = query.Where(user => (user.Status ?? ActiveStatus).ToLower() == loweredStatus);
        }

        int total = await query.CountAsync();
        List<User> items = await query
            .OrderByDescending(user => user.CreatedAt)
            .ThenBy(user => user.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public Task<User?> GetUserWithRolesAsync(Guid userId)
    {
        return _context.Users
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .ThenInclude(role => role.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .AsSplitQuery()
            .FirstOrDefaultAsync(user => user.Id == userId);
    }

    public Task<List<Role>> GetRolesByIdsAsync(IReadOnlyCollection<Guid> roleIds)
    {
        return _context.Roles
            .Include(role => role.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .AsSplitQuery()
            .Where(role => roleIds.Contains(role.Id))
            .ToListAsync();
    }

    public Task ReplaceUserRolesAsync(User user, IReadOnlyCollection<Role> roles)
    {
        HashSet<Guid> targetIds = roles.Select(role => role.Id).ToHashSet();
        List<UserRole> revoked = user.UserRoles
            .Where(userRole => !targetIds.Contains(userRole.RoleId))
            .ToList();
        foreach (UserRole userRole in revoked)
        {
            user.UserRoles.Remove(userRole);
            _context.UserRoles.Remove(userRole);
        }

        HashSet<Guid> existingIds = user.UserRoles.Select(userRole => userRole.RoleId).ToHashSet();
        foreach (Role role in roles.Where(role => !existingIds.Contains(role.Id)))
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                // assigned_at is timestamp without time zone — keep Kind=Unspecified.
                AssignedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            });
        }

        return Task.CompletedTask;
    }

    public async Task<(List<SystemLog> Items, int Total)> QuerySystemLogsAsync(
        string? search, Guid? userId, int page, int pageSize)
    {
        IQueryable<SystemLog> query = _context.SystemLogs
            .Include(log => log.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = $"%{search.Trim()}%";
            query = query.Where(log =>
                EF.Functions.ILike(log.Action ?? string.Empty, term)
                || EF.Functions.ILike(log.Description ?? string.Empty, term));
        }

        if (userId.HasValue)
        {
            query = query.Where(log => log.UserId == userId.Value);
        }

        int total = await query.CountAsync();
        List<SystemLog> items = await query
            .OrderByDescending(log => log.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task AddSystemLogAsync(SystemLog log)
    {
        await _context.SystemLogs.AddAsync(log);
    }

    public async Task<SysAdminOverviewCounts> GetOverviewCountsAsync(DateTime now)
    {
        DateTime weekAgo = now.AddDays(-7);
        DateTime closingSoonLimit = now.AddDays(10);

        return new SysAdminOverviewCounts
        {
            TotalUsers = await _context.Users.CountAsync(),
            ActiveUsers = await _context.Users.CountAsync(user => user.Status == null || user.Status == ActiveStatus),
            TotalRoles = await _context.Roles.CountAsync(),
            TotalPermissions = await _context.Permissions.CountAsync(),
            OpenJobs = await _context.Jobs.CountAsync(job => job.Status == JobStatus.Approved),
            JobsClosingSoon = await _context.Jobs.CountAsync(job =>
                job.Status == JobStatus.Approved
                && job.Deadline != null
                && job.Deadline > now
                && job.Deadline <= closingSoonLimit),
            PendingApprovalJobs = await _context.Jobs.CountAsync(job => job.Status == JobStatus.PendingApproval),
            TotalApplications = await _context.Applications.CountAsync(),
            ApplicationsLast7Days = await _context.Applications.CountAsync(application =>
                application.AppliedAt != null && application.AppliedAt >= weekAgo),
            UpcomingInterviews = await _context.Interviews.CountAsync(interview =>
                interview.Status == InterviewStatus.Scheduled && interview.InterviewDate >= now),
            EnabledWorkflows = await _context.WorkflowDefinitions.CountAsync(workflow => workflow.IsEnabled),
            ExecutionsLast7Days = await _context.WorkflowExecutions.CountAsync(execution => execution.CreatedAt >= weekAgo),
            FailedExecutionsLast7Days = await _context.WorkflowExecutions.CountAsync(execution =>
                execution.CreatedAt >= weekAgo
                && (execution.Status == WorkflowExecutionStatus.Failed || execution.Status == WorkflowExecutionStatus.DeadLetter)),
        };
    }
}
