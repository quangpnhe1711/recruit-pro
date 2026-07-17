using AutoMapper;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Common;
using RecruitPro.Application.Configurations;
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
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly JwtSettings _jwtSettings;
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
            IRefreshTokenRepository refreshTokenRepository,
            IJwtService jwtService,
            IEmailService emailService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IOptions<JwtSettings> jwtSettings)
        {
            _userRepository = userRepository;
            _candidateProfileRepository = candidateProfileRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _jwtService = jwtService;
            _emailService = emailService;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _jwtSettings = jwtSettings.Value;
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
                throw new BusinessAppException(ErrorCodes.InvalidCredentials, 401);
        }

            // Deactivated/blocked accounts must not authenticate — System Admin deactivation is real,
            // not just a badge in the user directory.
            if (user.Status != null && !user.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessAppException(ErrorCodes.AccountDisabled, 401);
            }

            var roles = user.UserRoles.Select(x => x.Role.Name).ToList();
            if (roleRule != null && !roles.Any(roleRule))
            {
                throw new BusinessAppException(ErrorCodes.PortalAccessDenied, 401);
            }

            if (roles.Any(role => role.Equals("Candidate", StringComparison.OrdinalIgnoreCase)))
            {
                user = await EnsureCandidateProfileAsync(user);
            }

            return ApiResponse<LoginResponseDto>.Ok(await IssueTokensAsync(user));
        }

        /// <summary>
        /// Rotates a refresh token: validates the stored hash is present, unexpired, and belongs to an
        /// Active account, then deletes it and issues a fresh access+refresh pair. A deactivated account's
        /// tokens were already deleted (revoke-all), so its refresh attempt simply finds nothing → 401.
        /// </summary>
        public async Task<ApiResponse<LoginResponseDto>> RefreshAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return ApiResponse<LoginResponseDto>.Unauthorized(ErrorCodes.Unauthenticated);
            }

            string hash = _jwtService.HashRefreshToken(refreshToken.Trim());
            RefreshToken? stored = await _refreshTokenRepository.GetByHashAsync(hash);
            if (stored == null || stored.ExpiryDate <= DbDateTime.Now)
            {
                if (stored != null)
                {
                    _refreshTokenRepository.Remove(stored);
                    await _unitOfWork.SaveChangesAsync();
                }

                return ApiResponse<LoginResponseDto>.Unauthorized(ErrorCodes.Unauthenticated);
            }

            User? user = await _userRepository.GetByIdAsync(stored.UserId);
            if (user == null || (user.Status != null && !user.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)))
            {
                _refreshTokenRepository.Remove(stored);
                await _unitOfWork.SaveChangesAsync();
                return ApiResponse<LoginResponseDto>.Unauthorized(ErrorCodes.AccountDisabled);
            }

            // Single-use rotation: the presented token is retired before a new pair is minted.
            _refreshTokenRepository.Remove(stored);
            return ApiResponse<LoginResponseDto>.Ok(await IssueTokensAsync(user));
        }

        public async Task<ApiResponse<string>> LogoutAsync(string? refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return ApiResponse<string>.Ok("Đã đăng xuất.");
            }

            string hash = _jwtService.HashRefreshToken(refreshToken.Trim());
            RefreshToken? stored = await _refreshTokenRepository.GetByHashAsync(hash);
            if (stored != null)
            {
                _refreshTokenRepository.Remove(stored);
                await _unitOfWork.SaveChangesAsync();
            }

            return ApiResponse<string>.Ok("Đã đăng xuất.");
        }

        /// <summary>
        /// Issues an access token (carrying the current TokenVersion) and a fresh refresh JWT,
        /// persisting only the refresh token's hash, then projects both into the login response.
        /// </summary>
        private async Task<LoginResponseDto> IssueTokensAsync(User user)
        {
            string accessToken = _jwtService.GenerateAccessToken(user);
            (string rawRefresh, string refreshHash) = _jwtService.CreateRefreshToken(user);

            await _refreshTokenRepository.AddAsync(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = refreshHash,
                ExpiryDate = DbDateTime.Now.AddMinutes(_jwtSettings.RefreshTokenExpiryMinutes),
            });
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<LoginResponseDto>(user, options =>
            {
                options.Items["AccessToken"] = accessToken;
                options.Items["RefreshToken"] = rawRefresh;
            });
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
                return ApiResponse<string>.BadRequest(ErrorCodes.Required);
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
