using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
