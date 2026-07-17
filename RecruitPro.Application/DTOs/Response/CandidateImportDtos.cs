namespace RecruitPro.Application.DTOs.Response;

public class CandidateImportPreviewDto
{
    public int RowNumber { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string PositionApplied { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
}

public class CandidateImportPreviewResponseDto
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public List<CandidateImportPreviewDto> Rows { get; set; } = [];
}

public class CandidateImportResultDto
{
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> CreatedCandidateIds { get; set; } = [];
    public List<string> InvitationEmails { get; set; } = [];

    // Per-row invitation outcome so HR can see which candidates did NOT receive their credentials and
    // resend to them. The account is always created; only the email may have failed.
    public List<CandidateImportInvitationDto> Invitations { get; set; } = [];
}

public class CandidateImportInvitationDto
{
    public int RowNumber { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CandidateId { get; set; } = string.Empty;
    public bool InvitationSent { get; set; }
}

public class CandidateImportTemplateDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public byte[] Content { get; set; } = [];
}
