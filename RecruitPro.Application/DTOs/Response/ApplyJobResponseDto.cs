namespace RecruitPro.Application.DTOs.Response;

public class ApplyJobResponseDto
{
    public string ApplicationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? RuleScore { get; set; }
    public decimal? SemanticScore { get; set; }
    public decimal? FinalScore { get; set; }
    public string? ScoreStatus { get; set; }
}
