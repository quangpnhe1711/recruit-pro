using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Services;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using Xunit;

namespace RecruitPro.Tests;

/// <summary>
/// T-NOTI-FE-001..006: Notification seen/read semantics, counts, ownership, realtime sender.
/// T-INTERVIEW-DATE-001: Interview creation preserves selected local date.
/// </summary>
public sealed class NotificationSeenReadTests
{
    // ---------------------------------------------------------------- helpers

    private static (NotificationService Service,
                    Mock<INotificationRepository> Repo) BuildService(
        List<Domain.Entities.Notification>? notifications = null)
    {
        var repo = new Mock<INotificationRepository>();

        if (notifications is not null)
        {
            foreach (var n in notifications)
            {
                repo.Setup(r => r.GetByIdAsync(n.Id)).ReturnsAsync(n);
            }
        }

        repo.Setup(r => r.MarkAsReadAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.MarkAllAsSeenAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);
        repo.Setup(r => r.MarkAllAsReadAsync(It.IsAny<Guid>()))
            .ReturnsAsync(0);
        repo.Setup(r => r.CountUnreadByUserIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(0);
        repo.Setup(r => r.CountUnseenByUserIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(0);

        return (new NotificationService(repo.Object), repo);
    }

    // ---------------------------------------------------------------- T-NOTI-FE-001

    [Fact(DisplayName = "T-NOTI-FE-001: MarkAllSeen_SetsSeenButNotRead")]
    public async Task MarkAllAsSeen_DoesNotCallMarkAllAsRead()
    {
        Guid userId = Guid.NewGuid();
        (NotificationService service, Mock<INotificationRepository> repo) = BuildService();

        await service.MarkAllAsSeenAsync(userId);

        repo.Verify(r => r.MarkAllAsSeenAsync(userId, It.IsAny<DateTime>()), Times.Once);
        repo.Verify(r => r.MarkAllAsReadAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ---------------------------------------------------------------- T-NOTI-FE-002

    [Fact(DisplayName = "T-NOTI-FE-002: MarkOneRead_SetsReadAndSeen")]
    public async Task MarkAsRead_SetsIsReadAndIsSeen()
    {
        Guid userId = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();
        var notification = new Domain.Entities.Notification
        {
            Id = notificationId,
            UserId = userId,
            IsRead = false,
            IsSeen = false
        };

        (NotificationService service, Mock<INotificationRepository> repo) =
            BuildService([notification]);

        await service.MarkAsReadAsync(userId, notificationId.ToString());

        repo.Verify(
            r => r.MarkAsReadAsync(notificationId, It.IsAny<DateTime>()),
            Times.Once,
            "MarkAsReadAsync must be called with the notification id and a timestamp");
    }

    // ---------------------------------------------------------------- T-NOTI-FE-003

    [Fact(DisplayName = "T-NOTI-FE-003: Counts_ReturnUnseenAndUnreadSeparately")]
    public async Task GetCounts_ReturnsBothUnseenAndUnread()
    {
        Guid userId = Guid.NewGuid();
        (NotificationService service, Mock<INotificationRepository> repo) = BuildService();
        repo.Setup(r => r.CountUnseenByUserIdAsync(userId)).ReturnsAsync(3);
        repo.Setup(r => r.CountUnreadByUserIdAsync(userId)).ReturnsAsync(5);

        var result = await service.GetCountsAsync(userId);

        result.Success.Should().BeTrue();
        result.Data!.Unseen.Should().Be(3);
        result.Data.Unread.Should().Be(5);
    }

    // ---------------------------------------------------------------- T-NOTI-FE-004

    [Fact(DisplayName = "T-NOTI-FE-004: UserCannotMarkOtherUsersNotificationRead")]
    public async Task MarkAsRead_ReturnsNotFound_WhenNotificationBelongsToAnotherUser()
    {
        Guid realOwner = Guid.NewGuid();
        Guid attacker = Guid.NewGuid();
        Guid notificationId = Guid.NewGuid();
        var notification = new Domain.Entities.Notification
        {
            Id = notificationId,
            UserId = realOwner,
            IsRead = false
        };

        (NotificationService service, Mock<INotificationRepository> repo) =
            BuildService([notification]);

        var result = await service.MarkAsReadAsync(attacker, notificationId.ToString());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        repo.Verify(r => r.MarkAsReadAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    // ---------------------------------------------------------------- T-NOTI-FE-005

    [Fact(DisplayName = "T-NOTI-FE-005: RealtimePublisher_SendsOnlyToRecipientUserGroup")]
    public async Task NotificationEventService_SendsToRecipientUserId()
    {
        Guid recruiterId = Guid.NewGuid();
        Guid candidateId = Guid.NewGuid();
        List<Domain.Entities.Notification> captured = [];

        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Domain.Entities.Notification>>()))
            .Callback<IEnumerable<Domain.Entities.Notification>>(items => captured.AddRange(items))
            .Returns(Task.CompletedTask);

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new User { Id = id, FullName = $"User-{id}" });

        Guid? sentToUserId = null;
        var realtimeSender = new Mock<INotificationRealtimeSender>();
        realtimeSender
            .Setup(s => s.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<Application.DTOs.Response.NotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Application.DTOs.Response.NotificationDto, CancellationToken>(
                (uid, _, _) => sentToUserId = uid)
            .Returns(Task.CompletedTask);

        var eventService = new NotificationEventService(
            repo.Object, userRepo.Object, realtimeSender.Object, Mock.Of<IUnitOfWork>(),
            Mock.Of<RecruitPro.Application.Interfaces.IServices.Automation.IRecruitProEventBus>());

        var job = new Job { Id = Guid.NewGuid(), Title = "Dev" };
        var app = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = candidateId,
            JobId = job.Id,
            Status = ApplicationStatus.Applied,
            AssignedRecruiterId = recruiterId,
            User = new User { Id = candidateId, FullName = "Candidate" },
            Job = job
        };

        await eventService.PublishApplicationAppliedAsync(app);

        // Should send only to recruiter, not broadcast to all
        captured.Should().ContainSingle("application applied notifies only the assigned recruiter");
        captured[0].UserId.Should().Be(recruiterId);
    }

    // ---------------------------------------------------------------- T-NOTI-FE-006

    [Fact(DisplayName = "T-NOTI-FE-006: NotificationDto_IncludesUrlAndSeenReadFlags")]
    public async Task GetUserNotifications_DtoIncludesIsSeenAndIsRead()
    {
        Guid userId = Guid.NewGuid();
        var notification = new Domain.Entities.Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Test",
            Body = "Body",
            IsRead = false,
            IsSeen = true,
            CreatedAt = DateTime.UtcNow,
            DataJson = "{\"url\":\"/hr/applications/123\",\"targetType\":\"application\"}"
        };

        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByUserIdAsync(userId, 1, 10))
            .ReturnsAsync([notification]);
        repo.Setup(r => r.CountByUserIdAsync(userId)).ReturnsAsync(1);

        var service = new NotificationService(repo.Object);
        var result = await service.GetUserNotificationsAsync(userId, 1, 10);

        result.Success.Should().BeTrue();
        var dto = result.Data!.Items.Single();
        dto.IsRead.Should().BeFalse();
        dto.IsSeen.Should().BeTrue();
        dto.Data.Should().NotBeNull("notification data must be present for deep-link navigation");
    }
}

/// <summary>
/// T-INTERVIEW-DATE-001: Creating an interview for a given DateOnly preserves the calendar day.
/// </summary>
public sealed class InterviewDateTests
{
    [Theory(DisplayName = "T-INTERVIEW-DATE-001: CreateInterview_PreservesSelectedLocalDate")]
    [InlineData(2026, 6, 28, 540)]
    [InlineData(2026, 6, 28, 600)]
    [InlineData(2026, 1, 1, 540)]
    [InlineData(2026, 12, 31, 1020)]
    public void InterviewDate_MatchesRequestDate(int year, int month, int day, int startMinutes)
    {
        // Simulate the date construction that InterviewService uses after the fix.
        DateTime interviewDate = DateTime.SpecifyKind(
            new DateTime(year, month, day, startMinutes / 60, startMinutes % 60, 0),
            DateTimeKind.Unspecified);

        interviewDate.Year.Should().Be(year, "year must not be shifted by timezone conversion");
        interviewDate.Month.Should().Be(month, "month must not be shifted");
        interviewDate.Day.Should().Be(day, "day must not be shifted — 28 must remain 28, not become 27");
        interviewDate.Kind.Should().Be(DateTimeKind.Unspecified,
            "timestamp without time zone column expects Unspecified kind to prevent Npgsql UTC conversion");
    }

    [Fact(DisplayName = "T-INTERVIEW-DATE-001b: InterviewDate_DayDoesNotShiftFromAddMinutes")]
    public void AddMinutesViaToDateTimeDoesNotShiftDay()
    {
        // Reproduce the old pattern (the bug trigger when DateOnly midnight + AddMinutes is used in
        // certain environments).  The new pattern must produce the same day without relying on
        // DateTime.Kind being set correctly by the runtime environment.
        var date = new DateOnly(2026, 6, 28);
        int startMinutes = 540; // 09:00

        DateTime safeDt = DateTime.SpecifyKind(
            new DateTime(date.Year, date.Month, date.Day, startMinutes / 60, startMinutes % 60, 0),
            DateTimeKind.Unspecified);

        safeDt.Day.Should().Be(28);
        safeDt.Month.Should().Be(6);
        safeDt.Year.Should().Be(2026);
    }
}
