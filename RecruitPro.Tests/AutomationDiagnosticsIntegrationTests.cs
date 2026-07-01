using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.Common;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;
using RecruitPro.Infrastructure.Data;
using RecruitPro.Tests.Infrastructure;
using Xunit;

namespace RecruitPro.Tests;

/// <summary>
/// v4 diagnostics integration tests over a real Postgres (Testcontainers). Proves the heartbeat is visible,
/// pending events are surfaced, and the per-workflow "no execution reason" is computed from real rows.
/// </summary>
public sealed class AutomationDiagnosticsIntegrationTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;

    public AutomationDiagnosticsIntegrationTests(PostgresTestFixture fixture) => _fixture = fixture;

    private async Task<AutomationWebAppFactory> ArrangeAsync()
    {
        var factory = new AutomationWebAppFactory(_fixture, WorkflowMode.Shadow);
        await factory.ResetAsync();
        return factory;
    }

    [Fact]
    public async Task Heartbeat_Upsert_IsVisibleAndHealthy()
    {
        using var factory = await ArrangeAsync();
        using var scope = factory.CreateServiceScope();
        var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var diagnostics = scope.ServiceProvider.GetRequiredService<IAutomationDiagnosticsService>();

        await workflows.UpsertHeartbeatAsync("dispatcher", DbDateTime.Now, "Running", "idle");
        await uow.SaveChangesAsync();

        var result = await diagnostics.GetGlobalAsync();

        result.Data!.Workers.Should().Contain(w => w.Name == "dispatcher" && !w.IsStale);
        result.Data.DispatcherHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task Global_Reports_PendingEvents()
    {
        using var factory = await ArrangeAsync();
        using var scope = factory.CreateServiceScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IEventOutboxRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var diagnostics = scope.ServiceProvider.GetRequiredService<IAutomationDiagnosticsService>();

        await outbox.AddAsync(new PublishedDomainEvent
        {
            Id = Guid.NewGuid(),
            EventType = WorkflowEventTypes.PassedToHeadReview,
            AggregateType = "Application",
            AggregateId = Guid.NewGuid(),
            DedupKey = "diag-pending-" + Guid.NewGuid(),
            PayloadJson = "{}",
            Status = WorkflowEventStatus.Pending,
            OccurredAt = DbDateTime.Now,
            CreatedAt = DbDateTime.Now,
        });
        await uow.SaveChangesAsync();

        var result = await diagnostics.GetGlobalAsync();

        result.Data!.PendingEvents.Should().BeGreaterThanOrEqualTo(1);
        result.Data.Warnings.Should().Contain(w => w.Contains("chờ xử lý"));
    }

    [Fact]
    public async Task Workflow_NoEventsToday_Reports_Reason()
    {
        using var factory = await ArrangeAsync();
        using var scope = factory.CreateServiceScope();
        var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var diagnostics = scope.ServiceProvider.GetRequiredService<IAutomationDiagnosticsService>();

        Guid defId = Guid.NewGuid();
        var def = new WorkflowDefinition { Id = defId, Name = "Diag Test WF " + defId, IsEnabled = true, CreatedAt = DbDateTime.Now };
        var version = new WorkflowDefinitionVersion
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = defId,
            VersionNo = 1,
            TriggerJson = $"{{\"eventType\":\"{WorkflowEventTypes.JobApproved}\"}}",
            ConditionsJson = "[]",
            ActionsJson = "[]",
            Mode = WorkflowMode.Shadow,
            IsActive = true,
            CreatedAt = DbDateTime.Now,
        };
        await workflows.AddDefinitionAsync(def);
        await workflows.AddVersionAsync(version);
        await uow.SaveChangesAsync();
        def.ActiveVersionId = version.Id;
        await uow.SaveChangesAsync();

        var result = await diagnostics.GetForWorkflowAsync(defId.ToString());

        result.Data!.HasActiveVersion.Should().BeTrue();
        result.Data.TriggerEventType.Should().Be(WorkflowEventTypes.JobApproved);
        // No JobApproved events seeded today → reason is the "no event yet" branch (unless env already has some).
        result.Data.NoExecutionReason.Should().NotBeNull();
    }
}
