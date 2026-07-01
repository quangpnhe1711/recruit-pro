using RecruitPro.Application.DTOs.Request.Automation;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Response.Automation;

namespace RecruitPro.Application.Interfaces.IServices.Automation;

public interface IWorkflowDefinitionService
{
    Task<ApiResponse<AutomationDashboardDto>> GetDashboardAsync();
    Task<ApiResponse<List<WorkflowSummaryDto>>> ListAsync(bool? isEnabled, string? triggerEventType, string? mode);
    Task<ApiResponse<WorkflowDetailDto>> GetAsync(string id);
    Task<ApiResponse<WorkflowDetailDto>> CreateAsync(CreateWorkflowRequest request, Guid? userId);
    Task<ApiResponse<WorkflowDetailDto>> UpdateAsync(string id, UpdateWorkflowRequest request, Guid? userId);
    Task<ApiResponse<WorkflowVersionDto>> PublishAsync(string id, Guid? userId);
    Task<ApiResponse<WorkflowDetailDto>> SetEnabledAsync(string id, bool isEnabled);
}

public interface IWorkflowExecutionService
{
    Task<ApiResponse<PaginatedResponseDto<ExecutionSummaryDto>>> QueryAsync(
        string? workflowDefinitionId, string? status, string? eventType, string? mode,
        DateTime? from, DateTime? to, int page, int pageSize);
    Task<ApiResponse<ExecutionDetailDto>> GetAsync(string id);
    Task<ApiResponse<ExecutionDetailDto>> RetryAsync(string id);
    Task<ApiResponse<PaginatedResponseDto<OutboxEventDto>>> QueryEventsAsync(
        string? status, string? eventType, DateTime? from, DateTime? to, int page, int pageSize);
    Task<ApiResponse<OutboxEventDto>> GetEventAsync(string id);
}
