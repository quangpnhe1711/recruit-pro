using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.API.Extensions;
using RecruitPro.API.Filters;
using RecruitPro.Application.DTOs.Request.SysAdmin;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

/// <summary>
/// System Admin → user directory, audit logs, and the console overview. Each endpoint is guarded by
/// the matching fine-grained permission so the RBAC matrix genuinely controls access.
/// </summary>
[ApiController]
[Authorize]
public class SysAdminDirectoryController : ControllerBase
{
    private readonly ISysAdminDirectoryService _directoryService;

    public SysAdminDirectoryController(ISysAdminDirectoryService directoryService)
    {
        _directoryService = directoryService;
    }

    [HttpGet("api/sysadmin/overview")]
    [RequirePermission("USER_VIEW")]
    public async Task<IActionResult> GetOverview()
    {
        var result = await _directoryService.GetOverviewAsync();
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/sysadmin/users")]
    [RequirePermission("USER_VIEW")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? q, [FromQuery] string? roleId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _directoryService.QueryUsersAsync(q, roleId, status, page, pageSize);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/sysadmin/users/{userId}/status")]
    [RequirePermission("USER_UPDATE")]
    public async Task<IActionResult> UpdateUserStatus(string userId, [FromBody] UpdateUserStatusRequest request)
    {
        var result = await _directoryService.UpdateUserStatusAsync(userId, request, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("api/sysadmin/users/{userId}/roles")]
    [RequirePermission("ROLE_MANAGE")]
    public async Task<IActionResult> UpdateUserRoles(string userId, [FromBody] UpdateUserRolesRequest request)
    {
        var result = await _directoryService.UpdateUserRolesAsync(userId, request, User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/sysadmin/audit-logs")]
    [RequirePermission("System_LOG_VIEW")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? q, [FromQuery] string? userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _directoryService.QuerySystemLogsAsync(q, userId, page, pageSize);
        return StatusCode(result.StatusCode, result);
    }
}
