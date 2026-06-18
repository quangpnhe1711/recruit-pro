namespace RecruitPro.Application.DTOs.Request.Discovery;

public class TalentPoolSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? JobId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public decimal? MinimumScore { get; set; }
}

public class CandidateDiscoveryRequest
{
    public string Query { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public decimal? MinimumScore { get; set; }
}
