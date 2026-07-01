using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;

namespace RecruitPro.Application.Services.Automation.Mcp;

/// <summary>
/// Shared, safe result-shaping for MCP tools. Never dumps full sensitive objects — only a compact status
/// summary — so a SystemAdmin auditing tool calls does not gain access to candidate/CV content that the
/// underlying application service (and its ownership checks) would otherwise gate.
/// </summary>
internal static class McpSummary
{
    public static object Of<T>(RecruitPro.Application.DTOs.Response.ApiResponse<T> response) => new
    {
        success = response.Success,
        statusCode = response.StatusCode,
        message = response.Message,
        hasData = response.Data is not null,
    };

    public static object Missing(string field) => new { success = false, statusCode = 400, message = $"'{field}' is required." };
}

public class JobsSearchTool : IMcpTool
{
    private readonly IJobService _jobs;
    public JobsSearchTool(IJobService jobs) => _jobs = jobs;
    public string Name => McpToolNames.JobsSearch;
    public string Description => "Tìm kiếm danh sách job đang mở (chỉ đọc).";
    public string Access => "read";
    public IReadOnlyList<string> PermissionsRequired => ["mcp.tools.view", "jobs.view"];
    public async Task<object?> InvokeAsync(McpToolCallContext context, CancellationToken cancellationToken = default)
        => McpSummary.Of(await _jobs.GetJobsAsync(1, 10));
}

public class JobsGetTool : IMcpTool
{
    private readonly IJobService _jobs;
    public JobsGetTool(IJobService jobs) => _jobs = jobs;
    public string Name => McpToolNames.JobsGet;
    public string Description => "Lấy chi tiết một job theo id (chỉ đọc).";
    public string Access => "read";
    public IReadOnlyList<string> PermissionsRequired => ["mcp.tools.view", "jobs.view"];
    public async Task<object?> InvokeAsync(McpToolCallContext context, CancellationToken cancellationToken = default)
    {
        string? jobId = GetString(context.Input, "jobId");
        return jobId is null ? McpSummary.Missing("jobId") : McpSummary.Of(await _jobs.GetJobDetailAsync(jobId));
    }

    internal static string? GetString(JsonElement input, string field)
        => input.ValueKind == JsonValueKind.Object && input.TryGetProperty(field, out JsonElement v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}

public class ApplicationsGetTool : IMcpTool
{
    private readonly IApplicationService _applications;
    public ApplicationsGetTool(IApplicationService applications) => _applications = applications;
    public string Name => McpToolNames.ApplicationsGet;
    public string Description => "Lấy chi tiết hồ sơ ứng tuyển (tuân thủ quyền sở hữu của service).";
    public string Access => "read";
    public IReadOnlyList<string> PermissionsRequired => ["mcp.tools.view", "applications.view"];
    public async Task<object?> InvokeAsync(McpToolCallContext context, CancellationToken cancellationToken = default)
    {
        string? applicationId = JobsGetTool.GetString(context.Input, "applicationId");
        if (applicationId is null)
        {
            return McpSummary.Missing("applicationId");
        }
        // Ownership is enforced by the application service using the caller identity — the tool never bypasses it.
        return McpSummary.Of(await _applications.GetApplicationReviewDetailAsync(applicationId, context.CallerUserId, context.Roles));
    }
}

public class ApplicationsGetFitAnalysisTool : IMcpTool
{
    private readonly IApplicationService _applications;
    public ApplicationsGetFitAnalysisTool(IApplicationService applications) => _applications = applications;
    public string Name => McpToolNames.ApplicationsGetFitAnalysis;
    public string Description => "Lấy tóm tắt mức độ phù hợp của hồ sơ (không trả CV đầy đủ).";
    public string Access => "read";
    public IReadOnlyList<string> PermissionsRequired => ["mcp.tools.view", "applications.view"];
    public async Task<object?> InvokeAsync(McpToolCallContext context, CancellationToken cancellationToken = default)
    {
        string? applicationId = JobsGetTool.GetString(context.Input, "applicationId");
        return applicationId is null
            ? McpSummary.Missing("applicationId")
            : McpSummary.Of(await _applications.GetApplicationReviewDetailAsync(applicationId, context.CallerUserId, context.Roles));
    }
}

public class InterviewsGetScheduleTool : IMcpTool
{
    private readonly IInterviewService _interviews;
    public InterviewsGetScheduleTool(IInterviewService interviews) => _interviews = interviews;
    public string Name => McpToolNames.InterviewsGetSchedule;
    public string Description => "Lấy dữ liệu lịch phỏng vấn (chỉ đọc).";
    public string Access => "read";
    public IReadOnlyList<string> PermissionsRequired => ["mcp.tools.view", "interviews.view"];
    public async Task<object?> InvokeAsync(McpToolCallContext context, CancellationToken cancellationToken = default)
    {
        string? applicationId = JobsGetTool.GetString(context.Input, "applicationId");
        return McpSummary.Of(await _interviews.GetScheduleDataAsync(applicationId));
    }
}

public class AnalyticsGetFunnelSummaryTool : IMcpTool
{
    private readonly IManagerAnalyticsService _analytics;
    public AnalyticsGetFunnelSummaryTool(IManagerAnalyticsService analytics) => _analytics = analytics;
    public string Name => McpToolNames.AnalyticsGetFunnelSummary;
    public string Description => "Tóm tắt phễu tuyển dụng tổng hợp (chỉ đọc).";
    public string Access => "read";
    public IReadOnlyList<string> PermissionsRequired => ["mcp.tools.view", "analytics.view"];
    public async Task<object?> InvokeAsync(McpToolCallContext context, CancellationToken cancellationToken = default)
        => McpSummary.Of(await _analytics.GetRecruitmentAnalyticsAsync());
}
