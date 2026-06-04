namespace RecruitPro.Application.DTOs.Response;

public class CandidateDashboardDto
{
    public string GreetingName { get; set; } = string.Empty;
    public CandidateDashboardStatsDto Stats { get; set; } = new();
    public UpcomingInterviewDto? UpcomingInterview { get; set; }
    public List<RecommendedJobDto> RecommendedJobs { get; set; } = [];
}
