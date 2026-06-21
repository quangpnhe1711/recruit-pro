namespace RecruitPro.Application.DTOs.Response;

public class ResumeUploadResponseDto
{
    public string ResumeId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public int Version { get; set; }
    public bool IsCurrent { get; set; }
    public string ParseStatus { get; set; } = string.Empty;
    public string? ParseMessage { get; set; }
    public DateTime? ParsedAt { get; set; }
    public List<string> ParserWarnings { get; set; } = [];
    public bool ProfileRefreshRequired { get; set; }
    public string? ProfileRefreshMessage { get; set; }
    public List<string> ProfileMismatchWarnings { get; set; } = [];
}
