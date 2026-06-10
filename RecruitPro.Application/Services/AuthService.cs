using AutoMapper;
using RecruitPro.Application.Common;
using RecruitPro.Application.DTOs.Request.Auth;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private const string CandidateLoginUrl = "http://localhost:5173/login";
        private const string InternalLoginUrl = "http://localhost:5173/internal/login";

        public AuthService(IUserRepository userRepository, IJwtService jwtService, IEmailService emailService, IUnitOfWork unitOfWork, IMapper mapper)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
            _emailService = emailService;
            _unitOfWork = unitOfWork;
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

        public Task<ApiResponse<string>> ForgotCandidatePasswordAsync(string email)
        {
            return ResetPasswordAsync(email, role => role.Equals("Candidate", StringComparison.OrdinalIgnoreCase), CandidateLoginUrl);
        }

        public Task<ApiResponse<string>> ForgotInternalPasswordAsync(string employeeIdOrEmail)
        {
            return ResetPasswordAsync(employeeIdOrEmail, role => !role.Equals("Candidate", StringComparison.OrdinalIgnoreCase), InternalLoginUrl);
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

        private async Task<ApiResponse<string>> ResetPasswordAsync(string identifier, Func<string, bool> roleRule, string loginUrl)
        {
            string normalizedIdentifier = identifier.Trim();
            if (string.IsNullOrWhiteSpace(normalizedIdentifier))
            {
                return ApiResponse<string>.BadRequest("Identifier is required.");
            }

            User? user = await _userRepository.GetTrackedByEmailAsync(normalizedIdentifier);
            if (user == null)
            {
                return ApiResponse<string>.Ok("If the account exists, a temporary password has been issued.");
            }

            List<string> roles = user.UserRoles.Select(x => x.Role.Name).ToList();
            if (!roles.Any(roleRule))
            {
                return ApiResponse<string>.Ok("If the account exists, a temporary password has been issued.");
            }

            string temporaryPassword = GenerateTemporaryPassword();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
            user.UpdatedAt = DbDateTime.Now;

            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
            await _emailService.SendPasswordResetAsync(user.Email, user.FullName, temporaryPassword, loginUrl);

            return ApiResponse<string>.Ok("If the account exists, a temporary password has been issued.");
        }

        private static string GenerateTemporaryPassword()
        {
            return $"Rp!{Guid.NewGuid():N}"[..12];
        }
    }
}
