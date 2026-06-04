namespace RecruitPro.Application.DTOs.Response;

public class HrJobsResponseDto
{
    public List<HrJobListItemDto> Items { get; set; } = [];
    public ApiEnvelopeMeta Meta { get; set; } = new();
    public HrJobStatsDto Stats { get; set; } = new();
}
