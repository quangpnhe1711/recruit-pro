using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.API.Extensions;
using RecruitPro.Application.DTOs.Request.Automation;
using RecruitPro.Application.Interfaces.IServices.Automation;

namespace RecruitPro.API.Controllers;

/// <summary>
/// SystemAdmin-only v4 Workflow Automation management. Normal HR/Manager users have no access here; they
/// only consume workflow outcomes through existing business screens and notifications.
/// </summary>
[ApiController]
[Authorize(Roles = "SystemAdmin")]
public class SysAdminAutomationController : ControllerBase
{
    private readonly IWorkflowDefinitionService _definitions;
    private readonly IWorkflowExecutionService _executions;

    public SysAdminAutomationController(IWorkflowDefinitionService definitions, IWorkflowExecutionService executions)
    {
        _definitions = definitions;
        _executions = executions;
    }

    [HttpGet("api/sysadmin/automation/dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _definitions.GetDashboardAsync();
        return StatusCode(result.StatusCode, result);
    }

    // ---- workflow definitions ----

    [HttpGet("api/sysadmin/automation/workflows")]
    public async Task<IActionResult> ListWorkflows([FromQuery] bool? isEnabled, [FromQuery] string? trigger, [FromQuery] string? mode)
    {
        var result = await _definitions.ListAsync(isEnabled, trigger, mode);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/sysadmin/automation/workflows/{id}")]
    public async Task<IActionResult> GetWorkflow(string id)
    {
        var result = await _definitions.GetAsync(id);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/sysadmin/automation/workflows")]
    public async Task<IActionResult> CreateWorkflow([FromBody] CreateWorkflowRequest request)
    {
        var result = await _definitions.CreateAsync(request, User.TryGetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/sysadmin/automation/workflows/{id}")]
    public async Task<IActionResult> UpdateWorkflow(string id, [FromBody] UpdateWorkflowRequest request)
    {
        var result = await _definitions.UpdateAsync(id, request, User.TryGetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/sysadmin/automation/workflows/{id}/publish")]
    public async Task<IActionResult> PublishWorkflow(string id)
    {
        var result = await _definitions.PublishAsync(id, User.TryGetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("api/sysadmin/automation/workflows/{id}/enabled")]
    public async Task<IActionResult> SetEnabled(string id, [FromBody] SetWorkflowEnabledRequest request)
    {
        var result = await _definitions.SetEnabledAsync(id, request.IsEnabled);
        return StatusCode(result.StatusCode, result);
    }

    // ---- executions ----

    [HttpGet("api/sysadmin/automation/executions")]
    public async Task<IActionResult> ListExecutions(
        [FromQuery] string? workflowId, [FromQuery] string? status, [FromQuery] string? eventType,
        [FromQuery] string? mode, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _executions.QueryAsync(workflowId, status, eventType, mode, from, to, page, pageSize);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/sysadmin/automation/executions/{executionId}")]
    public async Task<IActionResult> GetExecution(string executionId)
    {
        var result = await _executions.GetAsync(executionId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("api/sysadmin/automation/executions/{executionId}/retry")]
    public async Task<IActionResult> RetryExecution(string executionId)
    {
        var result = await _executions.RetryAsync(executionId);
        return StatusCode(result.StatusCode, result);
    }

    // ---- outbox debug ----

    [HttpGet("api/sysadmin/automation/events")]
    public async Task<IActionResult> ListEvents(
        [FromQuery] string? status, [FromQuery] string? eventType,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _executions.QueryEventsAsync(status, eventType, from, to, page, pageSize);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/sysadmin/automation/events/{id}")]
    public async Task<IActionResult> GetEvent(string id)
    {
        var result = await _executions.GetEventAsync(id);
        return StatusCode(result.StatusCode, result);
    }
}
