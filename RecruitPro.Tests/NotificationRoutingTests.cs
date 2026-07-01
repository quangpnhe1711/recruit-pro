using System.Text.Json;
using FluentAssertions;
using Moq;
using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Services;
using RecruitPro.Domain.Constants;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using Xunit;

namespace RecruitPro.Tests;

/// <summary>
/// Ownership-based notification routing + click-ready deep link coverage (TEST-MATRIX.md T-NOTI-001..023,
/// NOTIFICATION-EVENT-MATRIX.md). Recipients are deduplicated, candidate links are candidate-safe, and
/// every payload carries url/targetType/targetId. Best-effort behaviour is asserted at the service layer.
/// </summary>
public sealed class NotificationRoutingTests
{
    // ---------------------------------------------------------------- helpers

    private static (NotificationEventService Service, List<Notification> Captured) CreateService()
    {
        List<Notification> captured = [];
        var repository = new Mock<INotificationRepository>();
        repository.Setup(value => value.AddRangeAsync(It.IsAny<IEnumerable<Notification>>()))
            .Callback<IEnumerable<Notification>>(notifications => captured.AddRange(notifications))
            .Returns(Task.CompletedTask);

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(value => value.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new User { Id = id, FullName = $"User-{id}" });

        var service = new NotificationEventService(
            repository.Object,
            userRepository.Object,
            Mock.Of<INotificationRealtimeSender>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<RecruitPro.Application.Interfaces.IServices.Automation.IRecruitProEventBus>());

        return (service, captured);
    }

    private static (Domain.Entities.Application App, Guid RecruiterId, Guid HeadId, Guid CandidateId) BuildApp(
        ApplicationStatus status = ApplicationStatus.Applied)
    {
        Guid recruiterId = Guid.NewGuid();
        Guid headId = Guid.NewGuid();
        Guid candidateId = Guid.NewGuid();
        var job = new Job { Id = Guid.NewGuid(), Title = "Backend Engineer", DepartmentId = Guid.NewGuid() };
        var app = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = candidateId,
            JobId = job.Id,
            Status = status,
            AssignedRecruiterId = recruiterId,
            AssignedDepartmentHeadId = headId,
            DepartmentHeadReviewRequestedAt = DateTime.UtcNow,
            User = new User { Id = candidateId, FullName = "Nguyen Van A" },
            Job = job
        };
        return (app, recruiterId, headId, candidateId);
    }

    private static string? Data(Notification notification, string key)
    {
        using JsonDocument document = JsonDocument.Parse(notification.DataJson!);
        return document.RootElement.TryGetProperty(key, out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    private static Notification ForUser(IEnumerable<Notification> captured, Guid userId)
        => captured.Single(notification => notification.UserId == userId);

    // ---------------------------------------------------------------- T-NOTI-001 / 002 / 020

    [Fact] // T-NOTI-001
    public async Task ApplicationApplied_GoesToAssignedRecruiterOnly()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, Guid recruiterId, _, _) = BuildApp();

        await service.PublishApplicationAppliedAsync(app);

        captured.Should().ContainSingle();
        captured[0].UserId.Should().Be(recruiterId);
        captured[0].EventCode.Should().Be(NotificationEventCodes.ApplicationApplied);
    }

    [Fact] // T-NOTI-002
    public async Task ApplicationApplied_DoesNotNotifyDepartmentHead()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, _, Guid headId, _) = BuildApp();

        await service.PublishApplicationAppliedAsync(app);

        captured.Select(notification => notification.UserId).Should().NotContain(headId);
    }

    [Fact] // T-NOTI-020
    public async Task ApplicationApplied_UrlPointsToHrApplicationDetail()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, _, _, _) = BuildApp();

        await service.PublishApplicationAppliedAsync(app);

        Data(captured[0], "url").Should().Be($"/hr/applications/{app.Id}");
        Data(captured[0], "targetType").Should().Be(NotificationTargetTypes.Application);
        Data(captured[0], "targetId").Should().Be(app.Id.ToString());
    }

    // ---------------------------------------------------------------- T-NOTI-003 / 021

    [Fact] // T-NOTI-003
    public async Task ScreeningToHeadReview_NotifiesAssignedDepartmentHead()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, _, Guid headId, _) = BuildApp(ApplicationStatus.ManagerReview);

        await service.PublishDepartmentHeadReviewRequestedAsync(app, Guid.NewGuid());

        captured.Should().ContainSingle();
        captured[0].UserId.Should().Be(headId);
        captured[0].EventCode.Should().Be(NotificationEventCodes.ApplicationDepartmentHeadReviewRequested);
    }

    [Fact] // T-NOTI-021
    public async Task HeadReviewRequested_UrlPointsToDepartmentHeadReviewDetail()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, _, _, _) = BuildApp(ApplicationStatus.ManagerReview);

        await service.PublishDepartmentHeadReviewRequestedAsync(app, Guid.NewGuid());

        Data(captured[0], "url").Should().Be($"/manager/applications/{app.Id}");
        Data(captured[0], "targetType").Should().Be(NotificationTargetTypes.ApplicationReview);
    }

    // ---------------------------------------------------------------- T-NOTI-004

    [Fact] // T-NOTI-004
    public async Task HeadReviewToInterview_NotifiesRecruiterToScheduleInterview()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, Guid recruiterId, _, _) = BuildApp(ApplicationStatus.Interview);

        await service.PublishInterviewRequestedAsync(app);

        captured.Should().ContainSingle();
        captured[0].UserId.Should().Be(recruiterId);
        captured[0].EventCode.Should().Be(NotificationEventCodes.ApplicationInterviewRequested);
        Data(captured[0], "url").Should().Be($"/hr/interviews/schedule?applicationId={app.Id}");
        Data(captured[0], "targetType").Should().Be(NotificationTargetTypes.InterviewRequest);
    }

    // ---------------------------------------------------------------- T-NOTI-005 / 022

    [Fact] // T-NOTI-005
    public async Task InterviewScheduled_NotifiesCandidateRecruiterHeadAndInterviewer_Deduplicated()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, Guid recruiterId, Guid headId, Guid candidateId) = BuildApp(ApplicationStatus.Interview);
        var interview = new Interview { Id = Guid.NewGuid(), InterviewDate = new DateTime(2026, 2, 3, 9, 30, 0, DateTimeKind.Utc) };

        // Interviewer is the recruiter — the same user must not get two rows for the event.
        await service.PublishInterviewScheduledAsync(app, interview, recruiterId);

        captured.Should().HaveCount(3);
        captured.Select(notification => notification.UserId)
            .Should().BeEquivalentTo([candidateId, recruiterId, headId]);
        captured.Count(notification => notification.UserId == recruiterId).Should().Be(1);
        captured.Should().OnlyContain(notification => notification.EventCode == NotificationEventCodes.InterviewScheduled);
    }

    [Fact] // T-NOTI-022
    public async Task InterviewScheduled_CandidateUrlDoesNotPointToHrRoute()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, _, _, Guid candidateId) = BuildApp(ApplicationStatus.Interview);
        var interview = new Interview { Id = Guid.NewGuid(), InterviewDate = DateTime.UtcNow };

        await service.PublishInterviewScheduledAsync(app, interview, null);

        string? candidateUrl = Data(ForUser(captured, candidateId), "url");
        candidateUrl.Should().StartWith("/candidate/");
        candidateUrl.Should().NotContain("/hr/");
        candidateUrl.Should().NotContain("/manager/");
    }

    // ---------------------------------------------------------------- T-NOTI-006

    [Fact] // T-NOTI-006
    public async Task InterviewCompleted_NotifiesRecruiterAndHead()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, Guid recruiterId, Guid headId, Guid candidateId) = BuildApp(ApplicationStatus.Interview);
        var interview = new Interview { Id = Guid.NewGuid(), InterviewDate = DateTime.UtcNow };

        await service.PublishInterviewCompletedAsync(app, interview);

        captured.Select(notification => notification.UserId).Should().BeEquivalentTo([recruiterId, headId]);
        captured.Select(notification => notification.UserId).Should().NotContain(candidateId);
        captured.Should().OnlyContain(notification => notification.EventCode == NotificationEventCodes.InterviewCompleted);
    }

    // ---------------------------------------------------------------- T-NOTI-007 / 008

    [Fact] // T-NOTI-007
    public async Task WithdrawBeforeHeadReview_NotifiesRecruiterOnly()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, Guid recruiterId, Guid headId, _) = BuildApp(ApplicationStatus.Withdrawn);

        await service.PublishApplicationWithdrawnAsync(app, ApplicationStatus.Screening);

        captured.Should().ContainSingle();
        captured[0].UserId.Should().Be(recruiterId);
        captured.Select(notification => notification.UserId).Should().NotContain(headId);
    }

    [Fact] // T-NOTI-008
    public async Task WithdrawAfterHeadReview_NotifiesRecruiterAndHead()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, Guid recruiterId, Guid headId, _) = BuildApp(ApplicationStatus.Withdrawn);

        await service.PublishApplicationWithdrawnAsync(app, ApplicationStatus.Interview);

        captured.Select(notification => notification.UserId).Should().BeEquivalentTo([recruiterId, headId]);
        captured.Should().OnlyContain(notification => notification.EventCode == NotificationEventCodes.ApplicationWithdrawn);
    }

    // ---------------------------------------------------------------- T-NOTI-009 / 019

    private static Job BuildJob(out Guid recruiterId, out Guid headId)
    {
        recruiterId = Guid.NewGuid();
        headId = Guid.NewGuid();
        return new Job
        {
            Id = Guid.NewGuid(),
            Title = "Backend Engineer",
            CreatedBy = recruiterId,
            RecruiterId = recruiterId,
            DepartmentId = Guid.NewGuid(),
            Department = new Department { Id = Guid.NewGuid(), HeadUserId = headId }
        };
    }

    [Fact] // T-NOTI-009
    public async Task JobSubmittedForApproval_NotifiesDepartmentHead()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        Job job = BuildJob(out _, out Guid headId);

        await service.PublishJobSubmittedForApprovalAsync(job, Guid.NewGuid());

        captured.Should().ContainSingle();
        captured[0].UserId.Should().Be(headId);
        captured[0].EventCode.Should().Be(NotificationEventCodes.JobSubmittedForApproval);
    }

    [Fact] // T-NOTI-019
    public async Task JobSubmittedForApproval_UrlPointsToApprovalDetail()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        Job job = BuildJob(out _, out _);

        await service.PublishJobSubmittedForApprovalAsync(job, Guid.NewGuid());

        Data(captured[0], "url").Should().Be($"/manager/jobs/{job.Id}/approval");
        Data(captured[0], "targetType").Should().Be(NotificationTargetTypes.JobApproval);
        Data(captured[0], "targetId").Should().Be(job.Id.ToString());
    }

    [Fact] // T-NOTI-009b: no head configured -> no notification, no exception
    public async Task JobSubmittedForApproval_WhenNoDepartmentHead_PublishesNothing()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        var job = new Job { Id = Guid.NewGuid(), Title = "Backend", DepartmentId = Guid.NewGuid(), Department = new Department { Id = Guid.NewGuid() } };

        await service.PublishJobSubmittedForApprovalAsync(job, Guid.NewGuid());

        captured.Should().BeEmpty();
    }

    // ---------------------------------------------------------------- T-NOTI-010 / 011

    [Fact] // T-NOTI-010
    public async Task JobApproved_NotifiesRecruiter()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        Job job = BuildJob(out Guid recruiterId, out _);

        await service.PublishJobApprovedAsync(job);

        captured.Should().ContainSingle();
        captured[0].UserId.Should().Be(recruiterId);
        captured[0].EventCode.Should().Be(NotificationEventCodes.JobApproved);
        Data(captured[0], "url").Should().Be($"/jobs/{job.Id}");
    }

    [Fact] // T-NOTI-011
    public async Task JobRejected_NotifiesRecruiter()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        Job job = BuildJob(out Guid recruiterId, out _);

        await service.PublishJobRejectedAsync(job, "Headcount frozen");

        captured.Should().ContainSingle();
        captured[0].UserId.Should().Be(recruiterId);
        captured[0].EventCode.Should().Be(NotificationEventCodes.JobRejected);
        captured[0].Body.Should().Contain("Headcount frozen");
    }

    // ---------------------------------------------------------------- T-NOTI-012 / 023

    [Fact] // T-NOTI-012
    public async Task OfferEmailSent_NotifiesExpectedRecipients()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, Guid recruiterId, Guid headId, Guid candidateId) = BuildApp(ApplicationStatus.Offer);
        var offer = new ApplicationOffer { Id = Guid.NewGuid(), ApplicationId = app.Id };

        await service.PublishOfferEmailSentAsync(app, offer);

        captured.Select(notification => notification.UserId).Should().BeEquivalentTo([candidateId, recruiterId, headId]);
        captured.Should().OnlyContain(notification => notification.EventCode == NotificationEventCodes.OfferEmailSent);
    }

    [Fact] // T-NOTI-023
    public async Task OfferEmailSent_CandidateUrlPointsToCandidateOfferOrApplicationDetail()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, _, _, Guid candidateId) = BuildApp(ApplicationStatus.Offer);
        var offer = new ApplicationOffer { Id = Guid.NewGuid(), ApplicationId = app.Id };

        await service.PublishOfferEmailSentAsync(app, offer);

        string? candidateUrl = Data(ForUser(captured, candidateId), "url");
        candidateUrl.Should().StartWith("/candidate/my-applications");
        candidateUrl.Should().NotContain("/hr/");
    }

    // ---------------------------------------------------------------- T-NOTI-013

    [Fact] // T-NOTI-013
    public async Task RejectionEmailSent_NotifiesExpectedRecipients()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, Guid recruiterId, Guid headId, Guid candidateId) = BuildApp(ApplicationStatus.Rejected);

        await service.PublishRejectionEmailSentAsync(app);

        captured.Select(notification => notification.UserId).Should().BeEquivalentTo([candidateId, recruiterId, headId]);
        captured.Should().OnlyContain(notification => notification.EventCode == NotificationEventCodes.RejectionEmailSent);

        // Candidate-safe vs internal-safe links.
        Data(ForUser(captured, candidateId), "url").Should().StartWith("/candidate/");
        Data(ForUser(captured, recruiterId), "url").Should().Be($"/hr/applications/{app.Id}");
        Data(ForUser(captured, headId), "url").Should().Be($"/manager/applications/{app.Id}");
    }

    // ---------------------------------------------------------------- T-NOTI-014 / 015

    [Fact] // T-NOTI-014
    public async Task OfferAccepted_NotifiesRecruiterAndHead()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, Guid recruiterId, Guid headId, Guid candidateId) = BuildApp(ApplicationStatus.Hired);
        var offer = new ApplicationOffer { Id = Guid.NewGuid(), ApplicationId = app.Id };

        await service.PublishOfferAcceptedAsync(app, offer);

        captured.Select(notification => notification.UserId).Should().BeEquivalentTo([recruiterId, headId]);
        captured.Select(notification => notification.UserId).Should().NotContain(candidateId);
        captured.Should().OnlyContain(notification => notification.EventCode == NotificationEventCodes.OfferAccepted);
    }

    [Fact] // T-NOTI-015
    public async Task OfferDeclined_NotifiesRecruiterAndHead()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, Guid recruiterId, Guid headId, _) = BuildApp(ApplicationStatus.OfferDeclined);
        var offer = new ApplicationOffer { Id = Guid.NewGuid(), ApplicationId = app.Id };

        await service.PublishOfferDeclinedAsync(app, offer);

        captured.Select(notification => notification.UserId).Should().BeEquivalentTo([recruiterId, headId]);
        captured.Should().OnlyContain(notification => notification.EventCode == NotificationEventCodes.OfferDeclined);
    }

    // ---------------------------------------------------------------- T-NOTI-016

    [Fact] // T-NOTI-016
    public async Task NotificationFailure_DoesNotFailBusinessAction()
    {
        Guid interviewId = Guid.NewGuid();
        var interview = new Interview { Id = interviewId, ApplicationId = Guid.NewGuid(), Status = InterviewStatus.Scheduled };

        var interviewRepository = new Mock<IInterviewRepository>();
        interviewRepository.Setup(value => value.GetTrackedByIdAsync(interviewId)).ReturnsAsync(interview);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(value => value.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new Domain.Entities.Application
            {
                Id = interview.ApplicationId,
                User = new User { FullName = "Candidate" },
                Job = new Job { Title = "Backend" }
            });

        var notificationEventService = new Mock<INotificationEventService>();
        notificationEventService
            .Setup(value => value.PublishInterviewCompletedAsync(It.IsAny<Domain.Entities.Application>(), It.IsAny<Interview>()))
            .ThrowsAsync(new InvalidOperationException("notification backend unavailable"));

        var service = new InterviewService(
            interviewRepository.Object,
            applicationRepository.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<IUnitOfWork>(),
            notificationEventService.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InterviewService>.Instance,
            Mock.Of<AutoMapper.IMapper>());

        var response = await service.UpdateInterviewStatusAsync(interviewId.ToString(), new UpdateInterviewStatusRequest { Status = "completed" });

        // The committed status update must still succeed even though publishing threw.
        response.Success.Should().BeTrue();
        response.StatusCode.Should().Be(200);
    }

    // ---------------------------------------------------------------- T-NOTI-017

    [Fact] // T-NOTI-017
    public async Task RecipientDeduplication_NoDuplicateRowsForSameUserAndEvent()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        // Recruiter, head and interviewer are all the same internal user.
        Guid internalUserId = Guid.NewGuid();
        Guid candidateId = Guid.NewGuid();
        var job = new Job { Id = Guid.NewGuid(), Title = "Backend Engineer", DepartmentId = Guid.NewGuid() };
        var app = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = candidateId,
            JobId = job.Id,
            AssignedRecruiterId = internalUserId,
            AssignedDepartmentHeadId = internalUserId,
            User = new User { Id = candidateId, FullName = "Candidate" },
            Job = job
        };
        var interview = new Interview { Id = Guid.NewGuid(), InterviewDate = DateTime.UtcNow };

        await service.PublishInterviewScheduledAsync(app, interview, internalUserId);

        captured.Should().HaveCount(2);
        captured.Count(notification => notification.UserId == internalUserId).Should().Be(1);
        captured.Count(notification => notification.UserId == candidateId).Should().Be(1);
    }

    // ---------------------------------------------------------------- T-NOTI-018

    [Fact] // T-NOTI-018
    public async Task NotificationPayload_IncludesUrlTargetTypeAndTargetId()
    {
        (NotificationEventService service, List<Notification> captured) = CreateService();
        (Domain.Entities.Application app, _, _, _) = BuildApp();

        await service.PublishApplicationAppliedAsync(app);

        Notification notification = captured.Single();
        Data(notification, "url").Should().NotBeNullOrWhiteSpace();
        Data(notification, "targetType").Should().NotBeNullOrWhiteSpace();
        Data(notification, "targetId").Should().Be(app.Id.ToString());
        Data(notification, "eventCode").Should().Be(NotificationEventCodes.ApplicationApplied);
        // Deep links are frontend routes, never API routes.
        Data(notification, "url").Should().NotStartWith("/api/");

        // The deep-link metadata is also mirrored onto the entity columns for click-through.
        notification.EntityType.Should().Be(NotificationTargetTypes.Application);
        notification.EntityId.Should().Be(app.Id);
    }
}
