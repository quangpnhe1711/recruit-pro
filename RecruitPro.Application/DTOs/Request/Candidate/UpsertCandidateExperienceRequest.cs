namespace RecruitPro.Application.DTOs.Request.Candidate;

public class UpsertCandidateExperienceRequest
{
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public CandidateExperiencePeriodRequest Period { get; set; } = new();
    public List<string> Bullets { get; set; } = [];
}

public class CandidateExperiencePeriodRequest
{
    public int StartMonth { get; set; }
    public int StartYear { get; set; }
    public int? EndMonth { get; set; }
    public int? EndYear { get; set; }
    public bool IsCurrent { get; set; }
}
