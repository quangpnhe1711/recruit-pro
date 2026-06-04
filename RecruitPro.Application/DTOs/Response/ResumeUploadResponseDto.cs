namespace RecruitPro.Application.DTOs.Response;

public class ResumeUploadResponseDto
{
    public string ResumeId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
