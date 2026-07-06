using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

/// <summary>
/// v5.2/v5.4 — SystemAdmin AI Operations observability: metrics, telemetry list/detail, risk flags.
/// SystemAdmin-only (AI Ops is a system-administration surface, not business analytics — cf. the
/// intended fine-grained permission <c>ai.metrics.view</c> / <c>ai.risk_flags.view</c>). The runtime
/// boundary is the role gate; HR/Manager/Candidate cannot reach these endpoints.
/// </summary>
[ApiController]
[Authorize(Roles = "SystemAdmin")]
public class SysAdminAiOpsController : ControllerBase
{
    private readonly IAiOperationsMetricsService _metricsService;

    public SysAdminAiOpsController(IAiOperationsMetricsService metricsService) => _metricsService = metricsService;

    [HttpGet("api/sysadmin/ai/metrics")]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? feature, [FromQuery] string? provider, [FromQuery] string? model,
        CancellationToken cancellationToken)
    {
        var result = await _metricsService.GetMetricsAsync(from, to, feature, provider, model, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/sysadmin/ai/telemetry/recent")]
    public async Task<IActionResult> GetRecentTelemetry(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? feature, [FromQuery] string? provider, [FromQuery] string? model,
        [FromQuery] bool? success, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _metricsService.GetRecentAsync(
            from, to, feature, provider, model, success, onlyWithRiskFlags: false, page, pageSize, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/sysadmin/ai/telemetry/{id:guid}")]
    public async Task<IActionResult> GetTelemetryDetail(Guid id, CancellationToken cancellationToken)
    {
        var result = await _metricsService.GetDetailAsync(id, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/sysadmin/ai/risk-flags")]
    public async Task<IActionResult> GetRiskFlags(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? feature, [FromQuery] string? provider,
        CancellationToken cancellationToken)
    {
        var result = await _metricsService.GetRiskFlagsAsync(from, to, feature, provider, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/sysadmin/ai/risk-flags/recent")]
    public async Task<IActionResult> GetRecentRiskyRuns(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? feature, [FromQuery] string? provider, [FromQuery] string? model,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _metricsService.GetRecentAsync(
            from, to, feature, provider, model, success: null, onlyWithRiskFlags: true, page, pageSize, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
