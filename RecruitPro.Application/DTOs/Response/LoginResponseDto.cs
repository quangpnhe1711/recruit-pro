namespace RecruitPro.Application.DTOs.Response
{
    public sealed record LoginResponseDto
    {
        public UserDto User { get; init; } = null!;

        public string AccessToken { get; init; } = string.Empty;

        public string RefreshToken { get; init; } = string.Empty;
    }
}
