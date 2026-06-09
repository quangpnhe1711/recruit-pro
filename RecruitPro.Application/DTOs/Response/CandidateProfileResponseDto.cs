namespace RecruitPro.Application.DTOs.Response;

public class CandidateProfileResponseDto
{
    public CandidateProfileViewDto Profile { get; set; } = new();
    public List<CandidateSkillViewDto> Skills { get; set; } = [];
    public List<CandidateExperienceDto> ExperienceEntries { get; set; } = [];
    public CandidateResumeDto? Resume { get; set; }
}
