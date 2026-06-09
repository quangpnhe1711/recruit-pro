namespace RecruitPro.Application.DTOs.Response;

public class CandidateExperienceDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public CandidateExperiencePeriodDto Period { get; set; } = new();
    public List<string> Bullets { get; set; } = [];
}

public class CandidateExperiencePeriodDto
{
    public int StartMonth { get; set; }
    public int StartYear { get; set; }
    public int? EndMonth { get; set; }
    public int? EndYear { get; set; }
    public bool IsCurrent { get; set; }
}
