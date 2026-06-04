using AutoMapper;
using RecruitPro.Application.DTOs.Request.Auth;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;
        private readonly IMapper _mapper;

        public AuthService(IUserRepository userRepository, IJwtService jwtService, IMapper mapper)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
            _mapper = mapper;
        }

        public Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequest request)
        {
            return LoginCoreAsync(request.Email, request.Password, null);
        }

        public Task<ApiResponse<LoginResponseDto>> CandidateLoginAsync(string email, string password)
        {
            return LoginCoreAsync(email, password, role => role.Equals("Candidate", StringComparison.OrdinalIgnoreCase));
        }

        public Task<ApiResponse<LoginResponseDto>> InternalLoginAsync(string employeeIdOrEmail, string password)
        {
            return LoginCoreAsync(employeeIdOrEmail, password, role => !role.Equals("Candidate", StringComparison.OrdinalIgnoreCase));
        }

        private async Task<ApiResponse<LoginResponseDto>> LoginCoreAsync(string email, string password, Func<string, bool>? roleRule)
        {
            User? user = await _userRepository.GetByEmailAsync(email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                throw new UnauthorizeException("Invalid email or password.");
            }

            var roles = user.UserRoles.Select(x => x.Role.Name).ToList();
            if (roleRule != null && !roles.Any(roleRule))
            {
                throw new UnauthorizeException("User does not have permission to access this portal.");
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
