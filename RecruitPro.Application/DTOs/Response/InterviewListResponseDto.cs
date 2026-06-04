namespace RecruitPro.Application.DTOs.Response;

public class InterviewListResponseDto
{
    public List<InterviewListItemDto> Items { get; set; } = [];
    public ApiEnvelopeMeta Meta { get; set; } = new();
}
