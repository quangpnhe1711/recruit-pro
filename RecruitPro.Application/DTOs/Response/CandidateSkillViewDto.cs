namespace RecruitPro.Application.DTOs.Response;

public class CandidateSkillViewDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Active { get; set; }
    public decimal? YearsOfExperience { get; set; }
}
