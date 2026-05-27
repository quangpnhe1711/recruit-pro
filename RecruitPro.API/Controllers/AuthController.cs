using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Services;
using LoginRequest = RecruitPro.Application.DTOs.Request.LoginRequest;

namespace RecruitPro.API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase { 

        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [Route("login")]
        [HttpPost]
        public async Task<ApiResponse<LoginResponseDto>> Login(LoginRequest request)
        {
            return await _authService.LoginAsync(request);
        }
    }
}
