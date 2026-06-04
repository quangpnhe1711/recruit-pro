namespace RecruitPro.Application.DTOs.Request.Auth;

public class InternalLoginRequest
{
    public string EmployeeIdOrEmail { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
