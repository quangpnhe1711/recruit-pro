using Microsoft.Extensions.Configuration;
using RecruitPro.Application.Interfaces.IServices;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace RecruitPro.Infrastructure.Service
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration configuration;

        public JwtService(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public string GenerateAccessToken(Guid userId, string email, List<string> roles)
        {
            var claims = new List<Claim> {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString())
            };

            //Add role claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(
                    key,
                    SecurityAlgorithms.HmacSha256
                    );
            var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                Convert.ToInt32(
                    configuration["Jwt:ExpiryMinutes"]
                )
            ),
            signingCredentials: credentials
        );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);

        }

        public string GenerateRefreshToken()
        {
            throw new NotImplementedException();
        }
    }
}
