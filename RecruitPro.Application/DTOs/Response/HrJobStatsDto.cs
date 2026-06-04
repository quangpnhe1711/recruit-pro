namespace RecruitPro.Application.DTOs.Response;

public class HrJobStatsDto
{
    public int ActiveJobs { get; set; }
    public int PendingApproval { get; set; }
    public int TotalApplications { get; set; }
    public int TimeToHireDays { get; set; }
}
