namespace RecruitPro.Application.DTOs.Request.Candidate;

public class CandidateImportRequest
{
    public List<CandidateImportRowRequestDto> Rows { get; set; } = [];
}

public class CandidateImportRowRequestDto
{
    public int RowNumber { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string PositionApplied { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
