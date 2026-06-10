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
}

public class CandidateImportTemplateDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public byte[] Content { get; set; } = [];
}
