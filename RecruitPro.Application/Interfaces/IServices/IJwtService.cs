using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecruitPro.Application.Interfaces.IServices
{
    public interface IJwtService
    {
        string GenerateAccessToken(Guid userId, string email, List<string> roles);

        string GenerateRefreshToken();
    }
}
