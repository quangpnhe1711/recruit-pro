namespace RecruitPro.Application.DTOs.Request.Offers;

public class UpsertApplicationOfferRequest
{
    public string? OfferTemplateId { get; set; }
    public decimal BaseSalary { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public string? BonusDescription { get; set; }
    public string? EquityNotes { get; set; }
    public string EmploymentType { get; set; } = string.Empty;
    public DateTime? ProposedStartDate { get; set; }
    public string? ProbationPeriod { get; set; }
    public string? ReportingManagerId { get; set; }
    public string? PersonalMessage { get; set; }
    public List<string> BenefitIds { get; set; } = [];
}
