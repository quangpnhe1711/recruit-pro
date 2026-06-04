namespace RecruitPro.Application.DTOs.Request.Jobs;

public class PatchJobRequest
{
    public string? Title { get; set; }

    public string? Department { get; set; }

    public string? ApprovalStatus { get; set; }
}
