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

        /// <summary>
        /// Initializes a new instance of the AuthService class.
        /// </summary>
        /// <param name="userRepository">The <paramref name="userRepository"/> value.</param>
        /// <param name="jwtService">The <paramref name="jwtService"/> value.</param>
        /// <param name="emailService">The <paramref name="emailService"/> value.</param>
        /// <param name="unitOfWork">The <paramref name="unitOfWork"/> value.</param>
        /// <param name="mapper">The <paramref name="mapper"/> value.</param>
        public AuthService(IUserRepository userRepository, IJwtService jwtService, IEmailService emailService, IUnitOfWork unitOfWork, IMapper mapper)
        {
            _userRepository = userRepository;
            _jwtService = jwtService;
            _emailService = emailService;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        /// <summary>
        /// Logs in the requested data.
        /// </summary>
        /// <param name="request">The <paramref name="request"/> value.</param>
        /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
        public Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequest request)
        {
            return LoginCoreAsync(request.Email, request.Password, null);
        }

        /// <summary>
        /// Executes the candidate login operation.
        /// </summary>
        /// <param name="email">The <paramref name="email"/> value.</param>
        /// <param name="password">The <paramref name="password"/> value.</param>
        /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
        public Task<ApiResponse<LoginResponseDto>> CandidateLoginAsync(string email, string password)
        {
            return LoginCoreAsync(email, password, role => role.Equals("Candidate", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Executes the internal login operation.
        /// </summary>
        /// <param name="employeeIdOrEmail">The <paramref name="employeeIdOrEmail"/> value.</param>
        /// <param name="password">The <paramref name="password"/> value.</param>
        /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
        public Task<ApiResponse<LoginResponseDto>> InternalLoginAsync(string employeeIdOrEmail, string password)
        {
            return LoginCoreAsync(employeeIdOrEmail, password, role => !role.Equals("Candidate", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Executes the forgot candidate password operation.
        /// </summary>
        /// <param name="email">The <paramref name="email"/> value.</param>
        /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
        public Task<ApiResponse<string>> ForgotCandidatePasswordAsync(string email)
        {
            return ResetPasswordAsync(email, role => role.Equals("Candidate", StringComparison.OrdinalIgnoreCase), CandidateLoginUrl);
        }

        /// <summary>
        /// Executes the forgot internal password operation.
        /// </summary>
        /// <param name="employeeIdOrEmail">The <paramref name="employeeIdOrEmail"/> value.</param>
        /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
        public Task<ApiResponse<string>> ForgotInternalPasswordAsync(string employeeIdOrEmail)
        {
            return ResetPasswordAsync(employeeIdOrEmail, role => !role.Equals("Candidate", StringComparison.OrdinalIgnoreCase), InternalLoginUrl);
        }

        /// <summary>
        /// Logs in core.
        /// </summary>
        /// <param name="email">The <paramref name="email"/> value.</param>
        /// <param name="password">The <paramref name="password"/> value.</param>
        /// <param name="roleRule">The <paramref name="roleRule"/> value.</param>
        /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
        /// <exception cref="UnauthorizeException">Thrown when the operation fails validation or encounters an invalid state.</exception>
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

        /// <summary>
        /// Executes the reset password operation.
        /// </summary>
        /// <param name="identifier">The <paramref name="identifier"/> value.</param>
        /// <param name="roleRule">The <paramref name="roleRule"/> value.</param>
        /// <param name="loginUrl">The <paramref name="loginUrl"/> value.</param>
        /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
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

            string temporaryPassword = CredentialUtility.GenerateTemporaryPassword();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
            user.UpdatedAt = DbDateTime.Now;

            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
            await _emailService.SendPasswordResetAsync(user.Email, user.FullName, temporaryPassword, loginUrl);

            return ApiResponse<string>.Ok("If the account exists, a temporary password has been issued.");
        }

    }
}
