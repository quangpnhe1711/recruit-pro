namespace RecruitPro.Application.DTOs.Response;

public class ManagerReviewQueueItemDto
{
    public string ApplicationId { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateInitials { get; set; } = string.Empty;
    public string? CandidateAvatarUrl { get; set; }
    public string CandidateLocation { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? AppliedAt { get; set; }
    public int CompletedInterviews { get; set; }
    public int TotalInterviews { get; set; }
}

public class ManagerReviewQueueSummaryDto
{
    public int PendingFinalApprovals { get; set; }
    public int RecommendedCount { get; set; }
    public int FlaggedCount { get; set; }
    public double AverageScore { get; set; }
}

public class ManagerReviewQueueResponseDto
{
    public List<ManagerReviewQueueItemDto> Items { get; set; } = [];
    public ApiEnvelopeMeta Meta { get; set; } = new();
    public ManagerReviewQueueSummaryDto Summary { get; set; } = new();
}
