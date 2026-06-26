namespace RecruitPro.Application.DTOs.Response;

public class CandidateApplicationListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string CompanyOrDepartment { get; set; } = string.Empty;
    public DateTime? AppliedDate { get; set; }

    /// <summary>
    /// Canonical English ApplicationStatus enum value (Applied/Screening/ManagerReview/Interview/Offer/
    /// Hired/Rejected/OfferDeclined/Withdrawn). Business/logic field — never localized text.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Localized (Vietnamese) display label for <see cref="Status"/>. Presentation only — must not drive
    /// frontend logic. The frontend may render this or its own presentation label.
    /// </summary>
    public string StatusLabel { get; set; } = string.Empty;

    public string NextStep { get; set; } = string.Empty;
    public List<string> AvailableActions { get; set; } = [];
}
