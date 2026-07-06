using System.Collections.Generic;

namespace RecruitPro.Application.DTOs.Request.SysAdmin;

/// <summary>Full replacement of a role's granted permission codes (the matrix "Save" payload).</summary>
public class UpdateRolePermissionsRequest
{
    public List<string> PermissionCodes { get; set; } = new();
}

public class UpdateUserStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class UpdateUserRolesRequest
{
    public List<string> RoleIds { get; set; } = new();
}
