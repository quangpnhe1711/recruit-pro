using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.API.Extensions;
using RecruitPro.API.Filters;
using RecruitPro.Application.DTOs.Request.SysAdmin;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

/// <summary>
/// System Admin → RBAC permission matrix. Guarded by fine-grained permissions (not a hard-coded role):
/// any role granted PERMISSION_VIEW can read the matrix, PERMISSION_MANAGE is required to change it.
/// The seeded SystemAdmin role holds both.
/// </summary>
[ApiController]
[Authorize]
public class SysAdminRbacController : ControllerBase
{
    private readonly ISysAdminRbacService _rbacService;

    public SysAdminRbacController(ISysAdminRbacService rbacService)
    {
        _rbacService = rbacService;
    }

    [HttpGet("api/sysadmin/rbac/roles")]
    [RequirePermission("ROLE_VIEW")]
    public async Task<IActionResult> GetRoles()
    {
        var result = await _rbacService.GetRolesAsync();
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/sysadmin/rbac/modules")]
    [RequirePermission("PERMISSION_VIEW")]
    public IActionResult GetModules()
    {
        var result = _rbacService.GetModules();
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/sysadmin/rbac/roles/{roleId}/permissions")]
    [RequirePermission("PERMISSION_VIEW")]
    public async Task<IActionResult> GetRolePermissions(string roleId)
    {
        var result = await _rbacService.GetRolePermissionsAsync(roleId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("api/sysadmin/rbac/roles/{roleId}/permissions")]
    [RequirePermission("PERMISSION_MANAGE")]
    public async Task<IActionResult> UpdateRolePermissions(string roleId, [FromBody] UpdateRolePermissionsRequest request)
    {
        var result = await _rbacService.UpdateRolePermissionsAsync(roleId, request, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }
}
