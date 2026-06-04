namespace RecruitPro.Application.DTOs.Response;

public class HrDashboardStatsDto
{
    public int ActivePostings { get; set; }
    public int TotalApplicants { get; set; }
    public int InterviewsToday { get; set; }
    public string NextInterviewLabel { get; set; } = string.Empty;
}
