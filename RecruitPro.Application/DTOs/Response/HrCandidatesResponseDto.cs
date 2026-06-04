namespace RecruitPro.Application.DTOs.Response;

public class HrCandidatesResponseDto
{
    public List<HrCandidateListItemDto> Items { get; set; } = [];
    public ApiEnvelopeMeta Meta { get; set; } = new();
}
