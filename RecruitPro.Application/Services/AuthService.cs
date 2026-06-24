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
        private readonly ICandidateProfileRepository _candidateProfileRepository;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private const string CandidateLoginUrl = "http://localhost:5173/login";
        private const string InternalLoginUrl = "http://localhost:5173/internal/login";
        private const string ResumeParseStatusNotStarted = "NotStarted";
        private const string ResumeEmbeddingStatusNotStarted = "NotStarted";

        /// <summary>
        /// Initializes a new instance of the AuthService class.
        /// </summary>
        /// <param name="userRepository">The <paramref name="userRepository"/> value.</param>
        /// <param name="jwtService">The <paramref name="jwtService"/> value.</param>
        /// <param name="emailService">The <paramref name="emailService"/> value.</param>
        /// <param name="unitOfWork">The <paramref name="unitOfWork"/> value.</param>
        /// <param name="mapper">The <paramref name="mapper"/> value.</param>
        public AuthService(
            IUserRepository userRepository,
            ICandidateProfileRepository candidateProfileRepository,
            IJwtService jwtService,
            IEmailService emailService,
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _userRepository = userRepository;
            _candidateProfileRepository = candidateProfileRepository;
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
        public Task<ApiResponse<LoginResponseDto>> LoginAsync(string username, string password)
        {
            return LoginCoreAsync(username, password, null);
        }

        /// <summary>
        /// Executes the candidate login operation.
        /// </summary>
        /// <param name="username">The <paramref name="username"/> value.</param>
        /// <param name="password">The <paramref name="password"/> value.</param>
        /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
        public Task<ApiResponse<LoginResponseDto>> CandidateLoginAsync(string username, string password)
        {
            return LoginCoreAsync(username, password, role => role.Equals("Candidate", StringComparison.OrdinalIgnoreCase), candidateLogin: true);
        }

        /// <summary>
        /// Executes the internal login operation.
        /// </summary>
        /// <param name="username">The <paramref name="username"/> value.</param>
        /// <param name="password">The <paramref name="password"/> value.</param>
        /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
        public Task<ApiResponse<LoginResponseDto>> InternalLoginAsync(string username, string password)
        {
            return LoginCoreAsync(username, password, role => !role.Equals("Candidate", StringComparison.OrdinalIgnoreCase));
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
        /// <param name="identifier">The <paramref name="identifier"/> value.</param>
        /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
        public Task<ApiResponse<string>> ForgotInternalPasswordAsync(string identifier)
        {
            return ResetPasswordAsync(identifier, role => !role.Equals("Candidate", StringComparison.OrdinalIgnoreCase), InternalLoginUrl);
        }

        /// <summary>
        /// Logs in core.
        /// </summary>
        /// <param name="identifier">The <paramref name="identifier"/> value.</param>
        /// <param name="password">The <paramref name="password"/> value.</param>
        /// <param name="roleRule">The <paramref name="roleRule"/> value.</param>
        /// <param name="candidateLogin">Whether the candidate login flow should require username lookup.</param>
        /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
        /// <exception cref="UnauthorizeException">Thrown when the operation fails validation or encounters an invalid state.</exception>
        private async Task<ApiResponse<LoginResponseDto>> LoginCoreAsync(string identifier, string password, Func<string, bool>? roleRule, bool candidateLogin = false)
        {
            string normalizedIdentifier = identifier.Trim();
            User? user = candidateLogin
                ? await _userRepository.GetByUsernameAsync(normalizedIdentifier)
                : await _userRepository.GetByEmailOrUsernameAsync(normalizedIdentifier);

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                throw new UnauthorizeException(candidateLogin
                    ? "Username hoặc mật khẩu không đúng."
                    : "Username hoặc mật khẩu không đúng.");
        }

            var roles = user.UserRoles.Select(x => x.Role.Name).ToList();
            if (roleRule != null && !roles.Any(roleRule))
            {
                throw new UnauthorizeException("Tài khoản không có quyền truy cập cổng này.");
            }

            if (roles.Any(role => role.Equals("Candidate", StringComparison.OrdinalIgnoreCase)))
            {
                user = await EnsureCandidateProfileAsync(user);
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

            User? user = await _userRepository.GetTrackedByEmailOrUsernameAsync(normalizedIdentifier);
            if (user == null)
            {
                return ApiResponse<string>.Ok("Nếu tài khoản tồn tại, mật khẩu tạm đã được cấp.");
            }

            List<string> roles = user.UserRoles.Select(x => x.Role.Name).ToList();
            if (!roles.Any(roleRule))
            {
                return ApiResponse<string>.Ok("Nếu tài khoản tồn tại, mật khẩu tạm đã được cấp.");
            }

            string temporaryPassword = CredentialUtility.GenerateTemporaryPassword();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
            user.UpdatedAt = DbDateTime.Now;

            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
            await _emailService.SendPasswordResetAsync(user.Email, user.FullName, temporaryPassword, loginUrl);

            return ApiResponse<string>.Ok("Nếu tài khoản tồn tại, mật khẩu tạm đã được cấp.");
        }

        private async Task<User> EnsureCandidateProfileAsync(User user)
        {
            if (user.CandidateProfile != null)
            {
                return user;
            }

            CandidateProfile profile = new()
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                User = user,
                ResumeParseStatus = ResumeParseStatusNotStarted,
                CandidateEmbeddingStatus = ResumeEmbeddingStatusNotStarted
            };

            user.CandidateProfile = profile;
            await _candidateProfileRepository.SaveAsync(profile);
            await _unitOfWork.SaveChangesAsync();

            return user;
        }

    }
}
