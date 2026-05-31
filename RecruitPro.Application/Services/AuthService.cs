using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using RecruitPro.Application.Exceptions;
using AutoMapper;
using RecruitPro.Application.DTOs.Request.Auth;

namespace RecruitPro.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;
        private readonly IMapper _mapper;

        public AuthService(
            IUserRepository userRepository,
            IJwtService jwtService,
            IMapper mapper)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
            _mapper = mapper;
        }


        public async Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequest request)
        {
            User? user = await _userRepository.GetByEmailAsync(request.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify( request.Password,user.PasswordHash))
            {
                throw new UnauthorizeException("Invalid email or password.");
            }

            var accessToken = _jwtService.GenerateToken(user, "Access");
            var refreshToken = _jwtService.GenerateToken(user, "Refresh");

            var loginResponseDto = _mapper.Map<LoginResponseDto>(user, options =>
            {
                options.Items["AccessToken"] = accessToken;
                options.Items["RefreshToken"] = refreshToken;
            });

            return ApiResponse<LoginResponseDto>.Ok(loginResponseDto);
        }
    }
}
