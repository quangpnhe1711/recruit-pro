using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RecruitPro.Application.DTOs.Request.Candidate;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces.IServices;

namespace RecruitPro.API.Controllers
{
    [Route("api/candidate/")]
    [ApiController]
    public class CandidateController : ControllerBase
    {
        private readonly ICandidateProfileService _candidateService;


        public CandidateController(RecruitPro.Application.Interfaces.IServices.ICandidateProfileService candidateService)
        {
            _candidateService = candidateService;
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> RegisterAsync([FromForm] CandidateRegisterRequest request, IFormFile? resume)
        {
            Stream? stream = null;
            string? fileName = null;
            string? contentType = null;

            if (resume != null)
            {
                stream = resume.OpenReadStream();
                fileName = resume.FileName;
                contentType = resume.ContentType;
            }

            var result = await _candidateService.RegisterAsync(request, stream, fileName, contentType);

            if (result.Success)
                return StatusCode(result.StatusCode, result.Data);

            return StatusCode(result.StatusCode, result.Message);
        } 
        
    }
}
