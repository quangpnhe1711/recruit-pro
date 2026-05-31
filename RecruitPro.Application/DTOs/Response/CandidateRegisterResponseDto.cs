using System;

namespace RecruitPro.Application.DTOs.Response
{
    public class CandidateRegisterResponseDto
    {
        public Guid UserId { get; set; }
        public Guid CandidateProfileId { get; set; }
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
    }
}
