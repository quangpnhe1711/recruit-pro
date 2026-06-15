namespace RecruitPro.Application.DTOs.Request.Copilot;

public class CopilotRuleCriterionRequestDto
{
    public string Label { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "contains";
    public string Value { get; set; } = string.Empty;
    public string Weight { get; set; } = "medium";
    public bool AutoReject { get; set; }
}
