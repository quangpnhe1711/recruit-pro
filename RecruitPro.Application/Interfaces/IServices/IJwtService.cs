using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces.IServices
{
    public interface IJwtService
    {
        string GenerateAccessToken(User user);

        string GenerateRefreshToken();
    }
}
