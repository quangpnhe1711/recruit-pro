namespace RecruitPro.Application.DTOs.Request.Copilot;

public class CopilotPromptRequest
{
    public Guid JobId { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public bool UseLatestRankingContext { get; set; } = true;
}
