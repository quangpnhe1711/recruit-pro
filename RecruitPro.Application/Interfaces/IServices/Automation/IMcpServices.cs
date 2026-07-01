using System.Text.Json;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Automation;

namespace RecruitPro.Application.Interfaces.IServices.Automation;

/// <summary>Identity + input for an internal MCP tool call. Ownership is enforced by the app service the tool calls.</summary>
public sealed class McpToolCallContext
{
    public Guid? CallerUserId { get; init; }
    public IReadOnlyCollection<string> Roles { get; init; } = [];
    public JsonElement Input { get; init; }
}

/// <summary>A read-only internal tool. Must call application services (never repositories) and honour ownership.</summary>
public interface IMcpTool
{
    string Name { get; }
    string Description { get; }
    string Access { get; }
    IReadOnlyList<string> PermissionsRequired { get; }
    Task<object?> InvokeAsync(McpToolCallContext context, CancellationToken cancellationToken = default);
}

public interface IMcpToolRegistry
{
    IReadOnlyList<IMcpTool> Tools { get; }
    IMcpTool? Resolve(string name);
}

public interface IMcpToolService
{
    Task<ApiResponse<List<McpToolDto>>> ListToolsAsync();
    Task<ApiResponse<McpToolResult>> InvokeAsync(
        string toolName, Guid? callerUserId, IReadOnlyCollection<string> roles, string inputJson);
}

public interface IMcpToolAuditService
{
    Task<ApiResponse<PaginatedResponseDto<McpAuditDto>>> QueryAsync(string? toolName, bool? allowed, int page, int pageSize);
    Task<ApiResponse<McpAuditDto>> GetAsync(string id);
}
