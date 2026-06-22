namespace RecruitPro.Application.DTOs.Response;

public class ResumeStreamResponseDto
{
    public string ResumeId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public Stream Content { get; set; } = Stream.Null;
}
