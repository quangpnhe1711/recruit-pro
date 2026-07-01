using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.API.Extensions;
using RecruitPro.Application.DTOs.Request.Automation;
using RecruitPro.Application.Interfaces.IServices.Automation;

namespace RecruitPro.API.Controllers;

/// <summary>SystemAdmin-only MCP tool catalog + audit log + internal test endpoint.</summary>
[ApiController]
[Authorize(Roles = "SystemAdmin")]
public class SysAdminMcpController : ControllerBase
{
    private readonly IMcpToolService _tools;
    private readonly IMcpToolAuditService _audits;

    public SysAdminMcpController(IMcpToolService tools, IMcpToolAuditService audits)
    {
        _tools = tools;
        _audits = audits;
    }

    [HttpGet("api/sysadmin/mcp/tools")]
    public async Task<IActionResult> ListTools()
    {
        var result = await _tools.ListToolsAsync();
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/sysadmin/mcp/tools/{toolName}/test")]
    public async Task<IActionResult> TestTool(string toolName, [FromBody] McpToolTestRequest request)
    {
        var result = await _tools.InvokeAsync(toolName, User.TryGetCurrentUserId(), User.GetRoles(), request.InputJson);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/sysadmin/mcp/audits")]
    public async Task<IActionResult> ListAudits(
        [FromQuery] string? toolName, [FromQuery] bool? allowed,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _audits.QueryAsync(toolName, allowed, page, pageSize);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/sysadmin/mcp/audits/{id}")]
    public async Task<IActionResult> GetAudit(string id)
    {
        var result = await _audits.GetAsync(id);
        return StatusCode(result.StatusCode, result);
    }
}
