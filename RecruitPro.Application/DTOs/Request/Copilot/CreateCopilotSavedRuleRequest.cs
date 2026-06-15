namespace RecruitPro.Application.DTOs.Request.Copilot;

public class CreateCopilotSavedRuleRequest
{
    public Guid JobId { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<CopilotRuleCriterionRequestDto> PriorityCriteria { get; set; } = [];
    public IReadOnlyList<CopilotRuleCriterionRequestDto> NegativeCriteria { get; set; } = [];
    public bool IsActive { get; set; } = true;
}
