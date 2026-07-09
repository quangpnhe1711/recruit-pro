using System;
using System.Linq;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Automation;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Services.Automation.Mcp;

public class McpToolAuditService : IMcpToolAuditService
{
    private readonly IMcpToolAuditRepository _repository;

    public McpToolAuditService(IMcpToolAuditRepository repository) => _repository = repository;

    public async Task<ApiResponse<PaginatedResponseDto<McpAuditDto>>> QueryAsync(string? toolName, bool? allowed, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var (items, total) = await _repository.QueryAsync(toolName, allowed, page, pageSize);
        return ApiResponse<PaginatedResponseDto<McpAuditDto>>.Ok(new PaginatedResponseDto<McpAuditDto>
        {
            Items = items.Select(ToDto).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = total,
        });
    }

    public async Task<ApiResponse<McpAuditDto>> GetAsync(string id)
    {
        if (!Guid.TryParse(id, out Guid guid))
        {
            return ApiResponse<McpAuditDto>.BadRequest(ErrorCodes.InvalidInput);
        }
        McpToolAudit? audit = await _repository.GetByIdAsync(guid);
        return audit is null
            ? ApiResponse<McpAuditDto>.NotFound(ErrorCodes.EntityNotFound)
            : ApiResponse<McpAuditDto>.Ok(ToDto(audit));
    }

    private static McpAuditDto ToDto(McpToolAudit a) => new()
    {
        Id = a.Id.ToString(),
        ToolName = a.ToolName,
        CallerUserId = a.CallerUserId?.ToString(),
        Allowed = a.Allowed,
        DeniedReason = a.DeniedReason,
        LatencyMs = a.LatencyMs,
        CreatedAt = a.CreatedAt,
        InputJson = a.InputJson,
        OutputSummaryJson = a.OutputSummaryJson,
    };
}
