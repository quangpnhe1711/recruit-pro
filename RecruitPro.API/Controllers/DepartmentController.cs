using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request.Departments;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
public class DepartmentController : ControllerBase
{
    private readonly IJobService _jobService;

    public DepartmentController(IJobService jobService)
    {
        _jobService = jobService;
    }

    // Lookup (used by job create/edit dropdowns). Now includes the department head (Phase 2/3).
    [HttpGet("api/departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var result = await _jobService.GetDepartmentsAsync();
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("api/departments/{departmentId}")]
    [Authorize(Roles = "HR,Manager,HeadDepartment,SystemAdmin")]
    public async Task<IActionResult> GetDepartment(string departmentId)
    {
        var result = await _jobService.GetDepartmentAsync(departmentId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("api/departments/{departmentId}")]
    [Authorize(Roles = "HR,Manager,HeadDepartment,SystemAdmin")]
    public async Task<IActionResult> UpdateDepartment(string departmentId, [FromBody] UpdateDepartmentRequest request)
    {
        var result = await _jobService.UpdateDepartmentAsync(departmentId, request);
        return StatusCode(result.StatusCode, result);
    }
}
