namespace RecruitPro.Application.DTOs.Response;

public class CandidateEducationDto
{
    public string Id { get; set; } = string.Empty;
    public string School { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public string? FieldOfStudy { get; set; }
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public string? Description { get; set; }
}
