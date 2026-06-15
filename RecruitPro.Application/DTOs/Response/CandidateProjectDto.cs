namespace RecruitPro.Application.DTOs.Response;

public class CandidateProjectDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Description { get; set; }
    public List<string> Technologies { get; set; } = [];
    public CandidateExperiencePeriodDto Period { get; set; } = new();
}
