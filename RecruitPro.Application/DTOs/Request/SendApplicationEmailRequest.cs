namespace RecruitPro.Application.DTOs.Request;

public class SendApplicationEmailRequest
{
    public string TemplateType { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Body { get; set; }
}
