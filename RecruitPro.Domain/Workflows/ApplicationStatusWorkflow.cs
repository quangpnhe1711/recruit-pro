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

    public static bool IsClosed(ApplicationStatus status)
    {
        return status is ApplicationStatus.Hired
            or ApplicationStatus.Rejected
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
