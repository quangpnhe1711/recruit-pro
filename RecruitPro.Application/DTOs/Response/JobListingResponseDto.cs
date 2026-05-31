namespace RecruitPro.Application.DTOs.Response
{
    public sealed record JobsListingResponseDto
    {
        public IReadOnlyCollection<JobCardDto> Jobs { get; init; } = [];

        public int Total { get; init; }

        public int Page { get; init; }

        public int Limit { get; init; }
    }
}
