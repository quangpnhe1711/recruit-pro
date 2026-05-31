using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.DTOs.Request.Candidate;
using System.IO;

namespace RecruitPro.Application.Interfaces.IServices
{
    public interface ICandidateProfileService
    {
        Task<ApiResponse<CandidateRegisterResponseDto>> RegisterAsync(CandidateRegisterRequest request, Stream? resumeStream, string? resumeFileName, string? resumeContentType = null);
    }
}
