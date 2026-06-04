namespace RecruitPro.Application.DTOs.Response;

public class UpcomingInterviewDto
{
    public string Id { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string InterviewerName { get; set; } = string.Empty;
    public string InterviewerTitle { get; set; } = string.Empty;
    public string? MeetingUrl { get; set; }
}
