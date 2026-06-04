namespace RecruitPro.Application.DTOs.Request.Auth;

public class CandidateLoginRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
