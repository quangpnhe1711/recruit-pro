namespace RecruitPro.Application.DTOs.Response;

public class ScheduleInterviewerDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public Dictionary<string, List<int>> BusySlotsByDate { get; set; } = new();
}
