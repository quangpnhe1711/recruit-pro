using RecruitPro.Domain.Enums;
using RecruitPro.Domain.Workflows;

namespace RecruitPro.Tests;

public sealed class ApplicationStatusWorkflowTests
{
    [Theory]
    [InlineData(ApplicationStatus.Applied, ApplicationStatus.Screening, true)]
    [InlineData(ApplicationStatus.Applied, ApplicationStatus.Rejected, true)]
    [InlineData(ApplicationStatus.Applied, ApplicationStatus.Hired, false)]
    [InlineData(ApplicationStatus.Offer, ApplicationStatus.Hired, true)]
    [InlineData(ApplicationStatus.Offer, ApplicationStatus.Rejected, false)]
    public void CanTransition_RespectsConfiguredWorkflow(ApplicationStatus current, ApplicationStatus target, bool expected)
    {
        ApplicationStatusWorkflow.CanTransition(current, target).Should().Be(expected);
    }

    [Fact]
    public void GetAllowedTransitions_ReturnsConfiguredStatuses()
    {
        var transitions = ApplicationStatusWorkflow.GetAllowedTransitions(ApplicationStatus.Screening);

        transitions.Should().BeEquivalentTo([ApplicationStatus.ManagerReview, ApplicationStatus.Rejected]);
    }

    [Theory]
    [InlineData(ApplicationStatus.Hired, true)]
    [InlineData(ApplicationStatus.Rejected, true)]
    [InlineData(ApplicationStatus.OfferDeclined, true)]
    [InlineData(ApplicationStatus.Withdrawn, true)]
    [InlineData(ApplicationStatus.Interview, false)]
    public void IsClosed_DetectsTerminalStates(ApplicationStatus status, bool expected)
    {
        ApplicationStatusWorkflow.IsClosed(status).Should().Be(expected);
    }

    [Theory]
    [InlineData(ApplicationStatus.Applied, true)]
    [InlineData(ApplicationStatus.Screening, true)]
    [InlineData(ApplicationStatus.ManagerReview, true)]
    [InlineData(ApplicationStatus.Interview, true)]
    [InlineData(ApplicationStatus.Offer, false)]
    [InlineData(ApplicationStatus.Withdrawn, false)]
    [InlineData(ApplicationStatus.Rejected, false)]
    public void CanCandidateWithdraw_MatchesPolicy(ApplicationStatus status, bool expected)
    {
        ApplicationStatusWorkflow.CanCandidateWithdraw(status).Should().Be(expected);
    }

    [Theory]
    [InlineData(ApplicationStatus.Offer, true)]
    [InlineData(ApplicationStatus.Hired, false)]
    public void CanCandidateRespondToOffer_OnlyWhenOfferPending(ApplicationStatus status, bool expected)
    {
        ApplicationStatusWorkflow.CanCandidateRespondToOffer(status).Should().Be(expected);
    }

    [Theory]
    [InlineData(ApplicationStatus.Offer, true)]
    [InlineData(ApplicationStatus.Hired, true)]
    [InlineData(ApplicationStatus.OfferDeclined, true)]
    [InlineData(ApplicationStatus.Interview, false)]
    public void CanPrepareOffer_MatchesExpectedStatuses(ApplicationStatus status, bool expected)
    {
        ApplicationStatusWorkflow.CanPrepareOffer(status).Should().Be(expected);
    }
}
