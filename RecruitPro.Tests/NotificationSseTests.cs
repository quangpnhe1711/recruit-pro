using Microsoft.Extensions.Logging;
using RecruitPro.API.Realtime;
using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.DTOs.Response;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Services;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using Xunit;

namespace RecruitPro.Tests;

/// <summary>
/// T-SSE-002..005: the SSE notification broker (user-scoped, multi-tab, non-blocking) and the
/// publish-after-save / best-effort guarantees that replaced SignalR.
/// </summary>
public sealed class NotificationSseTests
{
    private static InMemoryNotificationSseBroker NewBroker()
        => new(Mock.Of<ILogger<InMemoryNotificationSseBroker>>());

    private static NotificationDto Dto(string title = "Hello")
        => new() { Id = Guid.NewGuid(), Title = title, Body = "Body", CreatedAt = DateTime.UtcNow };

    // ---------------------------------------------------------------- T-SSE-002

    [Fact(DisplayName = "T-SSE-002: Broker_PublishesOnlyToTargetUser")]
    public async Task PublishAsync_DeliversOnlyToTargetUser()
    {
        InMemoryNotificationSseBroker broker = NewBroker();
        Guid userA = Guid.NewGuid();
        Guid userB = Guid.NewGuid();

        using NotificationSseSubscription subA = broker.Subscribe(userA);
        using NotificationSseSubscription subB = broker.Subscribe(userB);

        NotificationDto dto = Dto("for-A");
        await broker.PublishAsync(userA, dto);

        subA.Reader.TryRead(out NotificationDto? receivedByA).Should().BeTrue("the target user must receive the event");
        receivedByA!.Id.Should().Be(dto.Id);

        subB.Reader.TryRead(out _).Should().BeFalse("notifications must never leak to other users");
    }

    // ---------------------------------------------------------------- T-SSE-003

    [Fact(DisplayName = "T-SSE-003: Broker_SupportsMultipleTabsSameUser")]
    public async Task PublishAsync_FansOutToEveryTabOfTheSameUser()
    {
        InMemoryNotificationSseBroker broker = NewBroker();
        Guid userId = Guid.NewGuid();

        using NotificationSseSubscription tab1 = broker.Subscribe(userId);
        using NotificationSseSubscription tab2 = broker.Subscribe(userId);

        NotificationDto dto = Dto();
        await broker.PublishAsync(userId, dto);

        tab1.Reader.TryRead(out NotificationDto? r1).Should().BeTrue();
        tab2.Reader.TryRead(out NotificationDto? r2).Should().BeTrue();
        r1!.Id.Should().Be(dto.Id);
        r2!.Id.Should().Be(dto.Id);
    }

    [Fact(DisplayName = "T-SSE-003b: Broker_StopsDeliveringAfterTabDisposed")]
    public async Task DisposedSubscription_NoLongerReceivesEvents()
    {
        InMemoryNotificationSseBroker broker = NewBroker();
        Guid userId = Guid.NewGuid();

        NotificationSseSubscription tab = broker.Subscribe(userId);
        tab.Dispose();

        // Publishing to a user whose only tab has closed must be a no-op (and must not throw).
        await broker.PublishAsync(userId, Dto());

        tab.Reader.TryRead(out _).Should().BeFalse("a disposed/closed channel yields nothing");
    }

    // ---------------------------------------------------------------- T-SSE-004

    [Fact(DisplayName = "T-SSE-004: NotificationCreation_PublishesSseAfterDbSave")]
    public async Task PublishApplicationApplied_SavesToDbBeforePushingOverSse()
    {
        var order = new List<string>();

        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Domain.Entities.Notification>>()))
            .Callback(() => order.Add("db-add"))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync())
            .Callback(() => order.Add("db-save"))
            .ReturnsAsync(1);

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new User { Id = id, FullName = $"User-{id}" });

        var realtimeSender = new Mock<INotificationRealtimeSender>();
        realtimeSender
            .Setup(s => s.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("sse-send"))
            .Returns(Task.CompletedTask);

        var eventService = new NotificationEventService(
            repo.Object, userRepo.Object, realtimeSender.Object, unitOfWork.Object,
            Mock.Of<RecruitPro.Application.Interfaces.IServices.Automation.IRecruitProEventBus>());

        var job = new Job { Id = Guid.NewGuid(), Title = "Dev" };
        var application = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            JobId = job.Id,
            Status = ApplicationStatus.Applied,
            AssignedRecruiterId = Guid.NewGuid(),
            User = new User { Id = Guid.NewGuid(), FullName = "Candidate" },
            Job = job
        };

        await eventService.PublishApplicationAppliedAsync(application);

        order.Should().Equal(["db-add", "db-save", "sse-send"],
            "the notification row must be persisted before it is pushed over SSE");
    }

    // ---------------------------------------------------------------- T-SSE-005

    [Fact(DisplayName = "T-SSE-005: SsePublishFailure_DoesNotFailBusinessAction")]
    public async Task CreateInterview_StillSucceeds_WhenNotificationPublishThrows()
    {
        Guid applicationId = Guid.NewGuid();
        var application = new Domain.Entities.Application
        {
            Id = applicationId,
            UserId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            // ManagerReview is a valid stage to schedule from (INV-008).
            Status = ApplicationStatus.ManagerReview
        };

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(r => r.GetTrackedByIdAsync(applicationId)).ReturnsAsync(application);

        // The realtime/notification publish blows up (e.g. broker disposed, serialization error).
        var notificationEventService = new Mock<INotificationEventService>();
        notificationEventService
            .Setup(s => s.PublishInterviewScheduledAsync(
                It.IsAny<Domain.Entities.Application>(), It.IsAny<Interview>(), It.IsAny<Guid?>()))
            .ThrowsAsync(new InvalidOperationException("SSE publish failed"));

        var service = new InterviewService(
            Mock.Of<IInterviewRepository>(),
            applicationRepository.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<IUnitOfWork>(),
            notificationEventService.Object,
            Mock.Of<ILogger<InterviewService>>(),
            TestMapperFactory.Create());

        var request = new CreateInterviewRequest
        {
            ApplicationId = applicationId.ToString(),
            Date = new DateOnly(2026, 6, 28),
            StartMinutes = 540,
            DurationMinutes = 60,
            Mode = "video",
            LocationOrLink = "https://meet.example/abc",
            Status = "scheduled"
        };

        var response = await service.CreateInterviewAsync(request);

        response.Success.Should().BeTrue("a committed interview must not be turned into an error by a best-effort SSE publish failure");
        response.StatusCode.Should().Be(201);
        notificationEventService.Verify(
            s => s.PublishInterviewScheduledAsync(It.IsAny<Domain.Entities.Application>(), It.IsAny<Interview>(), It.IsAny<Guid?>()),
            Times.Once);
    }
}
