using System;
using System.Collections.Generic;

namespace RecruitPro.Application.DTOs.Response.SysAdmin;

public class RbacRoleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int UserCount { get; set; }
    public int PermissionCount { get; set; }
    public bool IsSystemAdmin { get; set; }
}

public class RbacActionDto
{
    public string Code { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCritical { get; set; }
}

public class RbacModuleDto
{
    public string Key { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public List<RbacActionDto> Actions { get; set; } = new();
}

public class RolePermissionsDto
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? RoleDescription { get; set; }
    public List<string> GrantedCodes { get; set; } = new();
}

public class SysAdminUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public List<RbacRoleRefDto> Roles { get; set; } = new();
}

public class RbacRoleRefDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class SystemLogDto
{
    public string Id { get; set; } = string.Empty;
    public string? Action { get; set; }
    public string? Description { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
}

public class SysAdminOverviewDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int TotalRoles { get; set; }
    public int TotalPermissions { get; set; }
    public int OpenJobs { get; set; }
    public int JobsClosingSoon { get; set; }
    public int PendingApprovalJobs { get; set; }
    public int TotalApplications { get; set; }
    public int ApplicationsLast7Days { get; set; }
    public int UpcomingInterviews { get; set; }
    public int EnabledWorkflows { get; set; }
    public int ExecutionsLast7Days { get; set; }
    public int FailedExecutionsLast7Days { get; set; }
    public List<SystemLogDto> RecentLogs { get; set; } = new();
    public List<RbacRoleDto> Roles { get; set; } = new();
}
