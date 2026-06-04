namespace RecruitPro.Application.DTOs.Response;

public class ApplicationSummaryDto
{
    public int TotalApplications { get; set; }
    public List<FunnelCountDto> Funnel { get; set; } = [];
}
