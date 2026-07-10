namespace RecruitPro.Application.DTOs.Request.Auth
{
    public sealed class RefreshRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}
