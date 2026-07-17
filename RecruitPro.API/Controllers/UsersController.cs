using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.API.Extensions;
using RecruitPro.Application.DTOs.Request.Users;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers;

[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    // The signed-in internal user's own profile (account info + roles). All four internal roles;
    // candidates are blocked at the [Authorize] boundary even though the SPA never routes them here.
    [HttpGet("api/internal/profile")]
    [Authorize(Roles = "HR,Manager,HeadDepartment,SystemAdmin")]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _userService.GetProfileAsync(User.GetCurrentUserId());
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("api/internal/profile")]
    [Authorize(Roles = "HR,Manager,HeadDepartment,SystemAdmin")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateInternalProfileRequest request)
    {
        var result = await _userService.UpdateProfileAsync(User.GetCurrentUserId(), request);
        return StatusCode(result.StatusCode, result);
    }

    // Recruiters (HR) + department heads (HeadDepartment) assignable as recruitment owners.
    // Candidates are never returned.
    [HttpGet("api/users/assignable-recruitment-owners")]
    [Authorize(Roles = "HR,HeadDepartment,SystemAdmin")]
    public async Task<IActionResult> GetAssignableRecruitmentOwners()
    {
        var result = await _userService.GetAssignableRecruitmentOwnersAsync();
        return StatusCode(result.StatusCode, result);
    }
}
