using RecruitPro.Application.DTOs.Request;
using RecruitPro.Application.DTOs.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecruitPro.Application.Interfaces.IServices
{
    public interface IAuthService
    {
        Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequest request);
    }
}
