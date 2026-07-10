namespace RecruitPro.API.OData;

// Flat, read-only projection of an Application for OData querying. Status exposed as string so filters
// read naturally (e.g. $filter=Status eq 'Interview').
public class ApplicationODataDto
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? AppliedAt { get; set; }
    public decimal? RuleScore { get; set; }
    public decimal? SemanticScore { get; set; }
    public decimal? FinalScore { get; set; }
    public string? ScoreStatus { get; set; }
}
