using RecruitPro.Application.DTOs.Request.Auth;
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
        Task<ApiResponse<LoginResponseDto>> LoginAsync(string username, string password);
        Task<ApiResponse<LoginResponseDto>> CandidateLoginAsync(string username, string password);
        Task<ApiResponse<LoginResponseDto>> InternalLoginAsync(string username, string password);
        Task<ApiResponse<string>> ForgotCandidatePasswordAsync(string email);
        Task<ApiResponse<string>> ForgotInternalPasswordAsync(string identifier);
    }
}
