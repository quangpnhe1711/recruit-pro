using RecruitPro.Application.DTOs.Response;
using RecruitPro.Domain.Entities;

namespace RecruitPro.Application.Interfaces;

public interface IResumeParsingAiProvider
{
    Task<ResumeParsingAiResult> TryParseResumeAsync(
        string extractedText,
        IReadOnlyList<Skill> availableSkills,
        CancellationToken cancellationToken = default);
}

public class ResumeParsingAiResult
{
    public bool UsedAi { get; set; }
    public string? ModelName { get; set; }
    public string? FailureReason { get; set; }
    public CandidateResumeAiParseDto? Data { get; set; }
}
