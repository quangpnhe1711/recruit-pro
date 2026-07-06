namespace RecruitPro.Domain.Entities;

/// <summary>
/// v5.4 — the outcome of running one <see cref="AiEvaluationCase"/> during one evaluation run.
/// Results of a single run share a <see cref="RunId"/> so a run can be viewed as a batch.
/// Evaluation executes asynchronously in the background; it never runs on a synchronous request path.
/// </summary>
public class AiEvaluationResult
{
    public Guid Id { get; set; }

    /// <summary>Groups all results produced by one <c>POST /evaluations/run</c> invocation.</summary>
    public Guid RunId { get; set; }

    public Guid CaseId { get; set; }

    public Guid? PromptVersionId { get; set; }
    public string? ProviderName { get; set; }
    public string? ModelName { get; set; }

    public decimal? Score { get; set; }
    public bool? Passed { get; set; }

    /// <summary>jsonb output produced for the case (optional).</summary>
    public string? OutputJson { get; set; }

    public string? ErrorMessage { get; set; }
    public int? LatencyMs { get; set; }

    public DateTime CreatedAt { get; set; }
}
