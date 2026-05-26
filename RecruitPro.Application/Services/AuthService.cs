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
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Mappings;
using AutoMapper;

namespace RecruitPro.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly IJwtService _jwtService;
        private readonly IMapper _mapper;

        public AuthService(
            IUserRepository userRepository,
            IJwtService jwtService,
            PasswordHasher<User> passwordHasher,
            IMapper mapper)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
            _passwordHasher = passwordHasher;
            _mapper = mapper;
        }


        public async Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequest request)
        {
            User? user = await _userRepository.GetByEmailAsync(request.Email);

            if (user == null || 
                _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            {
                throw new UnauthorizeException("Invalid email or password.");
            }

            var loginResponseDto = new LoginResponseDto
            {
                User = _mapper.Map<UserDto>(user),
                AccessToken = _jwtService.GenerateAccessToken(user)
            };

            return ApiResponse<LoginResponseDto>.Ok(loginResponseDto);
        }
    }
}
