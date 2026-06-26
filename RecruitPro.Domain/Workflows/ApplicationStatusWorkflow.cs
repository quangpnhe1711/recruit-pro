using RecruitPro.Domain.Enums;

namespace RecruitPro.Domain.Workflows;

public static class ApplicationStatusWorkflow
{
    private static readonly IReadOnlyDictionary<ApplicationStatus, ApplicationStatus[]> AllowedTransitions =
        new Dictionary<ApplicationStatus, ApplicationStatus[]>
        {
            [ApplicationStatus.Applied] = [ApplicationStatus.Screening, ApplicationStatus.Rejected],
            [ApplicationStatus.Screening] = [ApplicationStatus.ManagerReview, ApplicationStatus.Rejected],
            [ApplicationStatus.ManagerReview] = [ApplicationStatus.Interview, ApplicationStatus.Rejected],
            [ApplicationStatus.Interview] = [ApplicationStatus.Offer, ApplicationStatus.Rejected],
            [ApplicationStatus.Offer] = [ApplicationStatus.Hired, ApplicationStatus.OfferDeclined],
            [ApplicationStatus.Hired] = [],
            [ApplicationStatus.Rejected] = [],
            [ApplicationStatus.OfferDeclined] = [],
            [ApplicationStatus.Withdrawn] = [],
        };

    public static bool CanTransition(ApplicationStatus current, ApplicationStatus target)
    {
        return AllowedTransitions.TryGetValue(current, out ApplicationStatus[]? nextStatuses)
            && nextStatuses.Contains(target);
    }

    public static IReadOnlyList<ApplicationStatus> GetAllowedTransitions(ApplicationStatus current)
    {
        return AllowedTransitions.TryGetValue(current, out ApplicationStatus[]? nextStatuses)
            ? nextStatuses
            : [];
    }

    /// <summary>
    /// The active (non-closed) application states. A candidate inside any of these is still in the
    /// recruitment pipeline. Used for the "at most one active application per (candidate, job)"
    /// invariant (INV-003) and the DB partial unique index (INV-014).
    /// </summary>
    public static readonly IReadOnlyList<ApplicationStatus> ActiveStatuses =
    [
        ApplicationStatus.Applied,
        ApplicationStatus.Screening,
        ApplicationStatus.ManagerReview,
        ApplicationStatus.Interview,
        ApplicationStatus.Offer,
    ];

    public static bool IsClosed(ApplicationStatus status)
    {
        return status is ApplicationStatus.Hired
            or ApplicationStatus.Rejected
            or ApplicationStatus.OfferDeclined
            or ApplicationStatus.Withdrawn;
    }

    /// <summary>
    /// A closed application from which a candidate may start a NEW application for the same job.
    /// `Hired` is closed-for-workflow but terminal for that jobId (INV-015): a candidate already
    /// hired for a posting must not re-apply to it.
    /// </summary>
    public static bool IsReapplyEligibleClosedStatus(ApplicationStatus status)
    {
        return status is ApplicationStatus.Rejected
            or ApplicationStatus.OfferDeclined
            or ApplicationStatus.Withdrawn;
    }

    public static bool CanCandidateWithdraw(ApplicationStatus status)
    {
        return status is ApplicationStatus.Applied
            or ApplicationStatus.Screening
            or ApplicationStatus.ManagerReview
            or ApplicationStatus.Interview;
    }

    public static bool CanCandidateRespondToOffer(ApplicationStatus status)
    {
        return status == ApplicationStatus.Offer;
    }

    public static bool CanPrepareOffer(ApplicationStatus status)
    {
        return status is ApplicationStatus.Offer
            or ApplicationStatus.Hired
            or ApplicationStatus.OfferDeclined;
    }
}
