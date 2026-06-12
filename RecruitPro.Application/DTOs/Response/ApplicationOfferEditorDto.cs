namespace RecruitPro.Application.DTOs.Response;

public class OfferEditorApplicationSummaryDto
{
    public string ApplicationId { get; set; } = string.Empty;
    public string ReferenceCode { get; set; } = string.Empty;
    public string StageLabel { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string? CandidateAvatarUrl { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
}

public class OfferTemplateOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class OfferBenefitOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class OfferCurrencyOptionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
}

public class OfferReportingManagerOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class OfferMasterDataDto
{
    public List<OfferTemplateOptionDto> Templates { get; set; } = [];
    public List<OfferBenefitOptionDto> Benefits { get; set; } = [];
    public List<OfferCurrencyOptionDto> Currencies { get; set; } = [];
    public List<string> EmploymentTypes { get; set; } = [];
    public List<OfferReportingManagerOptionDto> ReportingManagers { get; set; } = [];
}

public class ApplicationOfferDetailDto
{
    public string? OfferId { get; set; }
    public string Status { get; set; } = "Draft";
    public string? OfferTemplateId { get; set; }
    public decimal BaseSalary { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public string? BonusDescription { get; set; }
    public string? EquityNotes { get; set; }
    public string EmploymentType { get; set; } = string.Empty;
    public DateTime? ProposedStartDate { get; set; }
    public string? ProbationPeriod { get; set; }
    public string? ReportingManagerId { get; set; }
    public string? ReportingManagerName { get; set; }
    public string? PersonalMessage { get; set; }
    public List<string> BenefitIds { get; set; } = [];
    public DateTime? SentAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ApplicationOfferEditorDto
{
    public OfferEditorApplicationSummaryDto Application { get; set; } = new();
    public ApplicationOfferDetailDto Offer { get; set; } = new();
    public OfferMasterDataDto MasterData { get; set; } = new();
}
