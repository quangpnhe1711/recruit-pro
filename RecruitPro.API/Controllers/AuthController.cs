using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request.Auth;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IServices;
using LoginRequest = RecruitPro.Application.DTOs.Request.Auth.LoginRequest;

namespace RecruitPro.API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request.Username, request.Password);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("candidate/login")]
        public async Task<IActionResult> CandidateLogin([FromBody] CandidateLoginRequest request)
        {
            var result = await _authService.CandidateLoginAsync(request.Username, request.Password);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("internal/login")]
        public async Task<IActionResult> InternalLogin([FromBody] InternalLoginRequest request)
        {
            var result = await _authService.InternalLoginAsync(request.Username, request.Password);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            var result = await _authService.RefreshAsync(request.RefreshToken);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            var result = await _authService.LogoutAsync(request.RefreshToken);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("candidate/forgot-password")]
        public async Task<IActionResult> CandidateForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var result = await _authService.ForgotCandidatePasswordAsync(request.Identifier);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("internal/forgot-password")]
        public async Task<IActionResult> InternalForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var result = await _authService.ForgotInternalPasswordAsync(request.Identifier);
            return StatusCode(result.StatusCode, result);
        }
    }
}
