using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using FluentAssertions;
using Moq;
using RecruitPro.Application.Automation;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Application.Services.Automation;
using RecruitPro.Application.Services.Automation.Handlers;
using RecruitPro.Domain.Automation;
using RecruitPro.Domain.Entities;
using Xunit;

namespace RecruitPro.Tests;

/// <summary>Pure-logic + mocked unit tests for the v4 workflow engine (no DB, no Docker).</summary>
public class AutomationUnitTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    // ---------------- dedup ----------------

    [Fact]
    public void Dedup_ForTransition_IsDeterministic()
    {
        Guid id = Guid.NewGuid();
        string a = WorkflowDedup.ForTransition(WorkflowEventTypes.PassedToHeadReview, "app", id);
        string b = WorkflowDedup.ForTransition(WorkflowEventTypes.PassedToHeadReview, "app", id);
        a.Should().Be(b);
        a.Should().Contain("PassedToHeadReview");
    }

    [Fact]
    public void Dedup_ForWindow_ChangesWithWindow()
    {
        Guid id = Guid.NewGuid();
        string day1 = WorkflowDedup.ForWindow(WorkflowEventTypes.HeadReviewOverdue, "app", id, new DateTime(2026, 7, 1));
        string day2 = WorkflowDedup.ForWindow(WorkflowEventTypes.HeadReviewOverdue, "app", id, new DateTime(2026, 7, 2));
        day1.Should().NotBe(day2);
    }

    // ---------------- reminder cooldown ----------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReminderCooldown_Respects_Window(bool within)
    {
        DateTime now = new(2026, 7, 1, 12, 0, 0);
        DateTime lastSent = within ? now.AddHours(-1) : now.AddHours(-25);
        ReminderCooldown.IsWithinCooldown(lastSent, 24, now).Should().Be(within);
    }

    [Fact]
    public void ReminderCooldown_NeverSent_IsNotInCooldown()
        => ReminderCooldown.IsWithinCooldown(null, 24, DateTime.Now).Should().BeFalse();

    // ---------------- next-step suggestion ----------------

    [Theory]
    [InlineData(85, NextStepSuggestion.BandStrong)]
    [InlineData(60, NextStepSuggestion.BandMedium)]
    [InlineData(20, NextStepSuggestion.BandLow)]
    public void NextStepSuggestion_Bands(decimal score, string expectedBand)
        => NextStepSuggestion.Compute(score).Band.Should().Be(expectedBand);

    [Fact]
    public void NextStepSuggestion_NoScore_FallsBackDeterministically()
    {
        (string band, string suggestion) = NextStepSuggestion.Compute(null);
        band.Should().Be(NextStepSuggestion.BandUnknown);
        suggestion.Should().Contain("feedback");
    }

    // ---------------- condition evaluator ----------------

    private readonly WorkflowConditionEvaluator _evaluator = new();

    [Fact]
    public void Conditions_Empty_Passes()
        => _evaluator.Evaluate([], Json("{}"), DateTime.Now).Passed.Should().BeTrue();

    [Fact]
    public void Conditions_Exists_PassAndFail()
    {
        var cond = new List<WorkflowConditionModel> { new() { Field = "departmentHeadId", Operator = WorkflowConditionOperator.Exists } };
        _evaluator.Evaluate(cond, Json("{\"departmentHeadId\":\"x\"}"), DateTime.Now).Passed.Should().BeTrue();
        _evaluator.Evaluate(cond, Json("{}"), DateTime.Now).Passed.Should().BeFalse();
    }

    [Fact]
    public void Conditions_GreaterThanOrEqual_Numeric()
    {
        var cond = new List<WorkflowConditionModel> { new() { Field = "finalScore", Operator = WorkflowConditionOperator.GreaterThanOrEqual, Value = "80" } };
        _evaluator.Evaluate(cond, Json("{\"finalScore\":85}"), DateTime.Now).Passed.Should().BeTrue();
        _evaluator.Evaluate(cond, Json("{\"finalScore\":50}"), DateTime.Now).Passed.Should().BeFalse();
        _evaluator.Evaluate(cond, Json("{}"), DateTime.Now).Passed.Should().BeFalse();
    }

    [Fact]
    public void Conditions_InList_And_OlderThanDays()
    {
        var inList = new List<WorkflowConditionModel> { new() { Field = "status", Operator = WorkflowConditionOperator.InList, Value = "Completed,Passed" } };
        _evaluator.Evaluate(inList, Json("{\"status\":\"Completed\"}"), DateTime.Now).Passed.Should().BeTrue();
        _evaluator.Evaluate(inList, Json("{\"status\":\"Draft\"}"), DateTime.Now).Passed.Should().BeFalse();

        DateTime now = new(2026, 7, 10);
        var older = new List<WorkflowConditionModel> { new() { Field = "requestedAt", Operator = WorkflowConditionOperator.OlderThanDays, Value = "3" } };
        _evaluator.Evaluate(older, Json("{\"requestedAt\":\"2026-07-01T00:00:00\"}"), now).Passed.Should().BeTrue();
        _evaluator.Evaluate(older, Json("{\"requestedAt\":\"2026-07-09T00:00:00\"}"), now).Passed.Should().BeFalse();
    }

    // ---------------- action registry ----------------

    [Fact]
    public void ActionRegistry_ResolvesRegistered_AndNullForUnknown()
    {
        var dispatcher = Mock.Of<IWorkflowNotificationDispatcher>();
        var registry = new WorkflowActionRegistry(new IWorkflowActionHandler[]
        {
            new ShadowLogActionHandler(),
            new NotifyUserActionHandler(dispatcher),
        });
        registry.Resolve(WorkflowActionType.ShadowLog).Should().NotBeNull();
        registry.Resolve(WorkflowActionType.NotifyUser).Should().NotBeNull();
        registry.Resolve("does_not_exist").Should().BeNull();
    }

    // ---------------- cutover mode resolution ----------------

    [Fact]
    public void Settings_Disabled_Globally_ResolvesDisabled()
    {
        var settings = new WorkflowAutomationSettings { Enabled = false, DefaultMode = "Live" };
        settings.ResolveMode(WorkflowEventTypes.CandidateApplied).Should().Be(WorkflowMode.Disabled);
    }

    [Fact]
    public void Settings_EventOverride_WinsOverDefault()
    {
        var settings = new WorkflowAutomationSettings
        {
            Enabled = true,
            DefaultMode = "Shadow",
            EventModes = new() { [WorkflowEventTypes.PassedToHeadReview] = "Live" }
        };
        settings.ResolveMode(WorkflowEventTypes.PassedToHeadReview).Should().Be(WorkflowMode.Live);
        settings.ResolveMode(WorkflowEventTypes.JobApproved).Should().Be(WorkflowMode.Shadow);
    }

    // ---------------- notify action: shadow vs live ----------------

    [Fact]
    public async Task NotifyUser_Shadow_RecordsWouldNotify_WithoutSendFlag()
    {
        Guid recruiter = Guid.NewGuid();
        var dispatcher = new Mock<IWorkflowNotificationDispatcher>();
        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), WorkflowMode.Shadow, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, string _, string _, string _, string? _, WorkflowMode _, CancellationToken _) => ids.ToList());

        var handler = new NotifyUserActionHandler(dispatcher.Object);
        var result = await handler.ExecuteAsync(new WorkflowActionContext
        {
            Mode = WorkflowMode.Shadow,
            EventType = WorkflowEventTypes.PassedToHeadReview,
            Payload = Json($"{{\"recruiterId\":\"{recruiter}\"}}"),
            Config = Json("{\"recipients\":[\"assignedRecruiter\"]}"),
            ExecutionId = Guid.NewGuid(),
        });

        result.Status.Should().Be(WorkflowStepStatus.Success);
        string output = JsonSerializer.Serialize(result.Output);
        output.Should().Contain("\"sent\":false");
        output.Should().Contain(recruiter.ToString());
    }

    [Fact]
    public async Task NotifyUser_Live_MarksSent()
    {
        Guid recruiter = Guid.NewGuid();
        var dispatcher = new Mock<IWorkflowNotificationDispatcher>();
        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), WorkflowMode.Live, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, string _, string _, string _, string? _, WorkflowMode _, CancellationToken _) => ids.ToList());

        var handler = new NotifyUserActionHandler(dispatcher.Object);
        var result = await handler.ExecuteAsync(new WorkflowActionContext
        {
            Mode = WorkflowMode.Live,
            EventType = WorkflowEventTypes.PassedToHeadReview,
            Payload = Json($"{{\"recruiterId\":\"{recruiter}\"}}"),
            Config = Json("{\"recipients\":[\"assignedRecruiter\"]}"),
            ExecutionId = Guid.NewGuid(),
        });

        JsonSerializer.Serialize(result.Output).Should().Contain("\"sent\":true");
    }

    [Fact]
    public async Task NotifyUser_NoRecipient_Skips()
    {
        var handler = new NotifyUserActionHandler(Mock.Of<IWorkflowNotificationDispatcher>());
        var result = await handler.ExecuteAsync(new WorkflowActionContext
        {
            Mode = WorkflowMode.Live,
            EventType = WorkflowEventTypes.CandidateApplied,
            Payload = Json("{}"),
            Config = Json("{\"recipients\":[\"assignedRecruiter\"]}"),
            ExecutionId = Guid.NewGuid(),
        });
        result.Status.Should().Be(WorkflowStepStatus.Skipped);
    }

    // ---------------- dispatcher: shadow does not persist, live persists ----------------

    private static WorkflowNotificationDispatcher BuildDispatcher(
        Mock<INotificationRepository> notif, Mock<IUnitOfWork> uow)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new User { Id = id, FullName = "U" });
        return new WorkflowNotificationDispatcher(notif.Object, users.Object, Mock.Of<INotificationRealtimeSender>(), uow.Object);
    }

    [Fact]
    public async Task Dispatcher_Shadow_DoesNotPersistOrSend()
    {
        var notif = new Mock<INotificationRepository>();
        var uow = new Mock<IUnitOfWork>();
        var dispatcher = BuildDispatcher(notif, uow);

        IReadOnlyList<Guid> recipients = await dispatcher.DispatchAsync(
            [Guid.NewGuid()], "code", "t", "b", null, WorkflowMode.Shadow);

        recipients.Should().HaveCount(1);
        notif.Verify(n => n.AddRangeAsync(It.IsAny<IEnumerable<Notification>>()), Times.Never);
    }

    [Fact]
    public async Task Dispatcher_Live_PersistsNotifications()
    {
        var notif = new Mock<INotificationRepository>();
        var uow = new Mock<IUnitOfWork>();
        var dispatcher = BuildDispatcher(notif, uow);

        await dispatcher.DispatchAsync([Guid.NewGuid()], "code", "t", "b", null, WorkflowMode.Live);

        notif.Verify(n => n.AddRangeAsync(It.IsAny<IEnumerable<Notification>>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    // ---------------- diagnostics no-execution reasoner ----------------

    private static string? Reason(
        bool enabled = true, bool wfEnabled = true, bool hasActive = true,
        WorkflowMode mode = WorkflowMode.Shadow, int eventsToday = 1, int execToday = 1,
        int pending = 0, int skipped = 0, int success = 1)
        => AutomationDiagnosticsReasoner.ResolveNoExecutionReason(
            enabled, wfEnabled, hasActive, mode, eventsToday, execToday, pending, skipped, success);

    [Fact]
    public void Reason_AutomationDisabled_First()
        => Reason(enabled: false, wfEnabled: false, hasActive: false).Should().Contain("tắt toàn hệ thống");

    [Fact]
    public void Reason_WorkflowDisabled()
        => Reason(wfEnabled: false).Should().Contain("Workflow đang bị tắt");

    [Fact]
    public void Reason_NoActiveVersion()
        => Reason(hasActive: false).Should().Contain("chưa có phiên bản active");

    [Fact]
    public void Reason_ModeDisabled()
        => Reason(mode: WorkflowMode.Disabled).Should().Contain("Disabled");

    [Fact]
    public void Reason_NoEventToday()
        => Reason(eventsToday: 0, execToday: 0).Should().Contain("Chưa có sự kiện loại này");

    [Fact]
    public void Reason_EventPendingNotProcessed()
        => Reason(eventsToday: 1, execToday: 0, pending: 1).Should().Contain("worker chưa xử lý");

    [Fact]
    public void Reason_ConditionFalse()
        => Reason(execToday: 1, skipped: 1, success: 0).Should().Contain("Điều kiện workflow không thỏa");

    [Fact]
    public void Reason_Healthy_ReturnsNull()
        => Reason(eventsToday: 2, execToday: 2, success: 2, skipped: 0).Should().BeNull();
}
