using Microsoft.Extensions.Options;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Entities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RecruitPro.Infrastructure.Service
{
    public class JwtService : IJwtService
    {
        // Claim name for the account's token generation. Matched against the DB on every request
        // (JwtExtension.OnTokenValidated) so a deactivated account's live tokens stop working at once.
        public const string TokenVersionClaim = "token_version";

        private readonly JwtSettings _jwtSettings;

        public JwtService(IOptions<JwtSettings> options)
        {
            _jwtSettings = options.Value;
        }

        /// <inheritdoc />
        public string GenerateAccessToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(TokenVersionClaim, user.TokenVersion.ToString()),
            };

            foreach (var role in user.UserRoles.Select(ur => ur.Role.Name))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(_jwtSettings.ExpiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <inheritdoc />
        public (string Raw, string Hash) CreateRefreshToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(TokenVersionClaim, user.TokenVersion.ToString()),
                new Claim("token_type", "refresh"),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(_jwtSettings.RefreshTokenExpiryMinutes),
                signingCredentials: credentials
            );

            string raw = new JwtSecurityTokenHandler().WriteToken(token);
            return (raw, HashRefreshToken(raw));
        }

        /// <inheritdoc />
        public string HashRefreshToken(string raw)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }

        /// <inheritdoc />
        public string CreateResetToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(TokenVersionClaim, user.TokenVersion.ToString()),
                new Claim("token_type", "reset"),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <inheritdoc />
        public (Guid UserId, int TokenVersion)? ValidateResetToken(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidAudience = _jwtSettings.Audience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(1),
            };

            try
            {
                // Use JsonWebTokenHandler (the same modern handler the ASP.NET JwtBearer middleware uses):
                // the legacy JwtSecurityTokenHandler mis-reads the "exp" claim on these tokens (ValidTo comes
                // back MinValue → IDX10225), so validate with the handler that parses them correctly.
                var handler = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler();
                TokenValidationResult result = handler.ValidateTokenAsync(raw, parameters).GetAwaiter().GetResult();
                if (!result.IsValid || result.SecurityToken is not Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jwt)
                {
                    return null;
                }

                if (!jwt.TryGetPayloadValue<string>("token_type", out string? tokenType) || tokenType != "reset")
                {
                    return null;
                }

                string? version = jwt.TryGetPayloadValue<string>(TokenVersionClaim, out string? v) ? v : null;
                if (!Guid.TryParse(jwt.Subject, out Guid userId) || !int.TryParse(version, out int tokenVersion))
                {
                    return null;
                }

                return (userId, tokenVersion);
            }
            catch
            {
                return null;
            }
        }
    }
}
