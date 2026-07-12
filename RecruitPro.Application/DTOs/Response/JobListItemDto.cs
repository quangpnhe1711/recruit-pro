namespace RecruitPro.Application.DTOs.Response;

public class JobListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ShortPitch { get; set; }
    public string Department { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string WorkMode { get; set; } = string.Empty;
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    // System is VND-only (offer_currencies seeds VND; CreateJobRequest defaults VND). Jobs carry no
    // currency column, so this default is what the public list/detail reports — it must be VND, not USD.
    public string Currency { get; set; } = "VND";
    public DateTime? PostedAt { get; set; }
    public List<string> Tags { get; set; } = [];
    public string ShortDescription { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
}
