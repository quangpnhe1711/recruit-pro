using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Domain.Workflows;

/// <summary>
/// The interview-completion state of an application, derived from its interview records. Gates the
/// post-interview decisions (Interview -> Offer / Interview -> Rejected): an application in the
/// Interview stage may only proceed to a decision once it has a completed interview.
/// </summary>
public enum InterviewCompletionState
{
    /// <summary>No actionable interview has been scheduled yet (none, or all Canceled).</summary>
    Required,

    /// <summary>At least one interview is scheduled but none has been completed yet.</summary>
    NotCompleted,

    /// <summary>At least one interview has been completed.</summary>
    Completed,
}

public static class InterviewWorkflow
{
    /// <summary>
    /// Derives the interview-completion state from an application's interviews. Canceled interviews are
    /// history and do not count as "scheduled"; a single completed interview satisfies the requirement.
    /// </summary>
    public static InterviewCompletionState EvaluateCompletion(IEnumerable<Interview> interviews)
    {
        List<Interview> actionable = interviews
            .Where(interview => interview.Status != InterviewStatus.Canceled)
            .ToList();

        if (actionable.Count == 0)
        {
            return InterviewCompletionState.Required;
        }

        return actionable.Any(interview => interview.Status == InterviewStatus.Completed)
            ? InterviewCompletionState.Completed
            : InterviewCompletionState.NotCompleted;
    }

    /// <summary>
    /// True when the application has at least one completed interview, i.e. a post-interview decision
    /// (Offer/Rejected) is allowed.
    /// </summary>
    public static bool HasCompletedInterview(IEnumerable<Interview> interviews)
        => EvaluateCompletion(interviews) == InterviewCompletionState.Completed;
}
