namespace RecruitPro.Application.DTOs.Response;

/// <summary>
/// Read-only offer view for the owning candidate. Never exposes Draft offers
/// (a draft is internal HR working state) nor HR master data / editor options.
/// </summary>
public class CandidateOfferViewDto
{
    public string ApplicationId { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    /// <summary>Sent | Accepted | Declined (Draft offers are never returned).</summary>
    public string Status { get; set; } = string.Empty;
    public decimal BaseSalary { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string? CurrencySymbol { get; set; }
    public string? BonusDescription { get; set; }
    public string? EquityNotes { get; set; }
    public string EmploymentType { get; set; } = string.Empty;
    public DateTime? ProposedStartDate { get; set; }
    public string? ProbationPeriod { get; set; }
    public string? ReportingManagerName { get; set; }
    public string? PersonalMessage { get; set; }
    public List<string> Benefits { get; set; } = [];
    public DateTime? SentAt { get; set; }
}
