namespace RecruitPro.Application.DTOs.Response;

public class CandidateApplicationsResponseDto
{
    public List<CandidateApplicationListItemDto> Items { get; set; } = [];
    public ApiEnvelopeMeta Meta { get; set; } = new();
    public CandidateApplicationSummaryDto Summary { get; set; } = new();
}
