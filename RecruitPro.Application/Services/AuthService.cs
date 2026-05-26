using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace RecruitPro.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly PasswordHasher<User> _passwordHasher = new PasswordHasher<User>();
        private readonly IJwtService _jwtService;

        public AuthService(IUserRepository userRepository, IJwtService jwtService)
        {
            this._userRepository = userRepository;
            _jwtService = jwtService;
        }

        public async Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequest request)
        {
            User user = await _userRepository.GetByEmailAsync(request.Email);

            if (user == null || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            {
                throw new Exception("Invalid email or password.");
            }

            var loginResponseDto = new LoginResponseDto();
            loginResponseDto.User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone,
                AvatarUrl = user.AvatarUrl,
                Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList()
            };
            loginResponseDto.AccessToken = _jwtService.GenerateAccessToken(user);
            var userDto = new UserDto();
            userDto.Id = user.Id;
            userDto.Email = user.Email;
            userDto.FullName = user.FullName;
            userDto.Phone = user.Phone;
            userDto.AvatarUrl = user.AvatarUrl;
            userDto.Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

            return ApiResponse<LoginResponseDto>.Ok(loginResponseDto);
        }
    }
}
