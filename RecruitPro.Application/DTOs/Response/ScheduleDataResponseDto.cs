namespace RecruitPro.Application.DTOs.Response;

public class ScheduleDataResponseDto
{
    public ScheduleCandidateDto Candidate { get; set; } = new();
    public List<ScheduleInterviewerDto> Interviewers { get; set; } = [];
    public List<int> SlotMinutes { get; set; } = [];
}
