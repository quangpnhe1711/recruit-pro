using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Automation;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;
using RecruitPro.Domain.Constants;

namespace RecruitPro.Application.Services.Automation.Mcp;

/// <summary>
/// Runs an internal MCP tool with RBAC + auditing. Every call (allowed or denied) writes an audit row.
/// The tool itself calls application services, so business-data ownership is enforced there; this layer
/// only gates who may invoke tools at all and records a compact, non-sensitive output summary.
/// </summary>
public class McpToolService : IMcpToolService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMcpToolRegistry _registry;
    private readonly IMcpToolAuditRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<McpToolService> _logger;

    public McpToolService(
        IMcpToolRegistry registry,
        IMcpToolAuditRepository auditRepository,
        IUnitOfWork unitOfWork,
        ILogger<McpToolService> logger)
    {
        _registry = registry;
        _auditRepository = auditRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<List<McpToolDto>>> ListToolsAsync()
    {
        List<McpToolDto> tools = [];
        foreach (IMcpTool tool in _registry.Tools)
        {
            McpToolAudit? last = await _auditRepository.GetLastForToolAsync(tool.Name);
            tools.Add(new McpToolDto
            {
                Name = tool.Name,
                Description = tool.Description,
                PermissionsRequired = tool.PermissionsRequired.ToList(),
                Access = tool.Access,
                Enabled = true,
                LastCalledAt = last?.CreatedAt,
            });
        }
        return ApiResponse<List<McpToolDto>>.Ok(tools);
    }

    public async Task<ApiResponse<McpToolResult>> InvokeAsync(
        string toolName, Guid? callerUserId, IReadOnlyCollection<string> roles, string inputJson)
    {
        JsonElement input = ParseInput(inputJson);
        var stopwatch = Stopwatch.StartNew();

        IMcpTool? tool = _registry.Resolve(toolName);
        if (tool is null)
        {
            await AuditAsync(toolName, callerUserId, inputJson, allowed: false, deniedReason: "Unknown tool", output: null, latencyMs: 0);
            return ApiResponse<McpToolResult>.NotFound(ErrorCodes.McpToolNotFound);
        }

        // RBAC: only SystemAdmin may invoke internal tools (belt-and-suspenders behind the controller gate).
        if (roles is null || !roles.Contains(RoleNames.SystemAdmin))
        {
            await AuditAsync(toolName, callerUserId, inputJson, allowed: false, deniedReason: "Caller lacks SystemAdmin", output: null, latencyMs: (int)stopwatch.ElapsedMilliseconds);
            return ApiResponse<McpToolResult>.Ok(new McpToolResult { Allowed = false, DeniedReason = "Bạn không có quyền gọi công cụ MCP." });
        }

        try
        {
            object? output = await tool.InvokeAsync(new McpToolCallContext
            {
                CallerUserId = callerUserId,
                Roles = roles.ToArray(),
                Input = input,
            });
            stopwatch.Stop();
            await AuditAsync(toolName, callerUserId, inputJson, allowed: true, deniedReason: null, output: output, latencyMs: (int)stopwatch.ElapsedMilliseconds);
            return ApiResponse<McpToolResult>.Ok(new McpToolResult { Allowed = true, Output = output });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "MCP tool {Tool} threw", toolName);
            object error = new { success = false, error = ex.Message };
            await AuditAsync(toolName, callerUserId, inputJson, allowed: true, deniedReason: null, output: error, latencyMs: (int)stopwatch.ElapsedMilliseconds);
            return ApiResponse<McpToolResult>.Ok(new McpToolResult { Allowed = true, Output = error });
        }
    }

    private async Task AuditAsync(
        string toolName, Guid? callerUserId, string inputJson, bool allowed, string? deniedReason, object? output, int latencyMs)
    {
        await _auditRepository.AddAsync(new McpToolAudit
        {
            Id = Guid.NewGuid(),
            ToolName = toolName,
            CallerUserId = callerUserId,
            InputJson = string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson,
            OutputSummaryJson = output is null ? null : JsonSerializer.Serialize(output, JsonOptions),
            Allowed = allowed,
            DeniedReason = deniedReason,
            LatencyMs = latencyMs,
            CreatedAt = DbDateTime.Now,
        });
        await _unitOfWork.SaveChangesAsync();
    }

    private static JsonElement ParseInput(string inputJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            using JsonDocument doc = JsonDocument.Parse("{}");
            return doc.RootElement.Clone();
        }
    }
}
