using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Services;
using LoginRequest = RecruitPro.Application.DTOs.Request.LoginRequest;

namespace RecruitPro.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase { 

        private readonly AuthService _authService;

        public AuthController(AuthService authService)
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
