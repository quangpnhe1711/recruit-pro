namespace RecruitPro.Application.DTOs.Response;

public class JobSearchResponseDto
{
    public List<JobListItemDto> Items { get; set; } = [];
    public ApiEnvelopeMeta Meta { get; set; } = new();
}
