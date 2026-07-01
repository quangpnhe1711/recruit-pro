using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.Automation;
using RecruitPro.Application.Common;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Interfaces.IServices.Automation;
using RecruitPro.Domain.Automation;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;
using RecruitPro.Tests.Infrastructure;
using Xunit;

namespace RecruitPro.Tests;

/// <summary>
/// v4 integration tests over a real Postgres (Testcontainers). Covers the durable outbox, the workflow
/// engine (shadow/live/skip/fail/dead-letter/retry), the overdue scheduler, SystemAdmin RBAC and MCP.
/// </summary>
public sealed class AutomationIntegrationTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;

    public AutomationIntegrationTests(PostgresTestFixture fixture) => _fixture = fixture;

    private async Task<AutomationWebAppFactory> ArrangeAsync(WorkflowMode mode = WorkflowMode.Shadow)
    {
        var factory = new AutomationWebAppFactory(_fixture, mode);
        await factory.ResetAsync();
        return factory;
    }

    private static async Task<RecruitPro.Domain.Entities.Application> LoadAppAsync(AppDbContext db)
        => await db.Applications.Include(a => a.Job).ThenInclude(j => j.Department)
            .Include(a => a.User).Include(a => a.Interviews)
            .FirstAsync(a => a.Id == TestDataSeeder.ApplicationId);

    private static object AppPayload(bool includeHead = true, decimal? finalScore = 88) => new
    {
        applicationId = TestDataSeeder.ApplicationId,
        jobId = TestDataSeeder.ApprovedJobId,
        candidateUserId = TestDataSeeder.CandidateUserId,
        recruiterId = TestDataSeeder.HrUserId,
        departmentHeadId = includeHead ? TestDataSeeder.ManagerUserId : (Guid?)null,
        finalScore,
        status = "ManagerReview",
    };

    // ============================== Outbox / hooks ==============================

    [Fact]
    public async Task Hook_PassedToHeadReview_WritesDurableEvent()
    {
        using var factory = await ArrangeAsync();
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationEventService>();

        var app = await LoadAppAsync(db);
        await notifications.PublishDepartmentHeadReviewRequestedAsync(app, TestDataSeeder.HrUserId);

        (await db.PublishedDomainEvents.CountAsync(e => e.EventType == WorkflowEventTypes.PassedToHeadReview))
            .Should().Be(1);
    }

    [Fact]
    public async Task Hook_CandidateApplied_And_InterviewCompleted_WriteEvents()
    {
        using var factory = await ArrangeAsync();
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationEventService>();

        var app = await LoadAppAsync(db);
        await notifications.PublishApplicationAppliedAsync(app);
        await notifications.PublishInterviewCompletedAsync(app, app.Interviews.First());

        (await db.PublishedDomainEvents.AnyAsync(e => e.EventType == WorkflowEventTypes.CandidateApplied)).Should().BeTrue();
        (await db.PublishedDomainEvents.AnyAsync(e => e.EventType == WorkflowEventTypes.InterviewCompleted)).Should().BeTrue();
    }

    [Fact]
    public async Task Outbox_DuplicateTransition_DoesNotCreateDuplicateEvent()
    {
        using var factory = await ArrangeAsync();
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationEventService>();

        var app = await LoadAppAsync(db);
        await notifications.PublishDepartmentHeadReviewRequestedAsync(app, TestDataSeeder.HrUserId);
        await notifications.PublishDepartmentHeadReviewRequestedAsync(app, TestDataSeeder.HrUserId);

        (await db.PublishedDomainEvents.CountAsync(e => e.EventType == WorkflowEventTypes.PassedToHeadReview))
            .Should().Be(1);
    }

    // ============================== Engine ==============================

    private static async Task<Guid> PublishAndGetEventIdAsync(IServiceScope scope, string eventType, object payload)
    {
        var bus = scope.ServiceProvider.GetRequiredService<IRecruitProEventBus>();
        string dedup = WorkflowDedup.ForTransition(eventType, "app", Guid.NewGuid());
        await bus.PublishAsync(eventType, "Application", TestDataSeeder.ApplicationId, dedup, payload, DateTime.Now);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PublishedDomainEvents.Where(e => e.DedupKey == dedup).Select(e => e.Id).FirstAsync();
    }

    private static async Task SeedTemplatesAsync(IServiceScope scope)
        => await scope.ServiceProvider.GetRequiredService<IWorkflowTemplateSeeder>().SeedAsync();

    [Fact]
    public async Task Engine_MatchingWorkflow_Shadow_SucceedsWithoutSending()
    {
        using var factory = await ArrangeAsync(WorkflowMode.Shadow);
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedTemplatesAsync(scope);

        Guid eventId = await PublishAndGetEventIdAsync(scope, WorkflowEventTypes.PassedToHeadReview, AppPayload());
        await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().ProcessEventAsync(eventId);

        var execution = await db.WorkflowExecutions.Include(e => e.Steps)
            .FirstAsync(e => e.TriggerEventType == WorkflowEventTypes.PassedToHeadReview);
        execution.Status.Should().Be(WorkflowExecutionStatus.Success);
        execution.Mode.Should().Be(WorkflowMode.Shadow);
        // Shadow must not send a real notification to the head.
        (await db.Notifications.CountAsync(n => n.UserId == TestDataSeeder.ManagerUserId)).Should().Be(0);
    }

    [Fact]
    public async Task Engine_ConditionFalse_ProducesSkippedExecution()
    {
        using var factory = await ArrangeAsync(WorkflowMode.Shadow);
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedTemplatesAsync(scope);

        // No departmentHeadId => "Pass CV → Notify Head Review" condition (exists) fails.
        Guid eventId = await PublishAndGetEventIdAsync(scope, WorkflowEventTypes.PassedToHeadReview, AppPayload(includeHead: false));
        await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().ProcessEventAsync(eventId);

        var execution = await db.WorkflowExecutions.FirstAsync(e => e.TriggerEventType == WorkflowEventTypes.PassedToHeadReview);
        execution.Status.Should().Be(WorkflowExecutionStatus.Skipped);
    }

    [Fact]
    public async Task Engine_NonMatchingEvent_ProducesNoExecution()
    {
        using var factory = await ArrangeAsync(WorkflowMode.Shadow);
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedTemplatesAsync(scope);

        // No seeded template triggers on JobApproved.
        Guid eventId = await PublishAndGetEventIdAsync(scope, WorkflowEventTypes.JobApproved, AppPayload());
        await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().ProcessEventAsync(eventId);

        (await db.WorkflowExecutions.CountAsync(e => e.TriggerEventType == WorkflowEventTypes.JobApproved)).Should().Be(0);
        (await db.PublishedDomainEvents.FirstAsync(e => e.Id == eventId)).Status.Should().Be(WorkflowEventStatus.Processed);
    }

    [Fact]
    public async Task Engine_Live_SendsRealNotification()
    {
        using var factory = await ArrangeAsync(WorkflowMode.Live);
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedTemplatesAsync(scope);

        Guid eventId = await PublishAndGetEventIdAsync(scope, WorkflowEventTypes.PassedToHeadReview, AppPayload());
        await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().ProcessEventAsync(eventId);

        var execution = await db.WorkflowExecutions.FirstAsync(e => e.TriggerEventType == WorkflowEventTypes.PassedToHeadReview);
        execution.Status.Should().Be(WorkflowExecutionStatus.Success);
        execution.Mode.Should().Be(WorkflowMode.Live);
        (await db.Notifications.CountAsync(n => n.UserId == TestDataSeeder.ManagerUserId)).Should().Be(1);
    }

    [Fact]
    public async Task PerEventCutover_Live_SkipsOldDirectNotification()
    {
        using var factory = await ArrangeAsync(WorkflowMode.Live);
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationEventService>();

        var app = await LoadAppAsync(db);
        // In Live mode the direct call must NOT create a notification (the workflow owns it instead).
        await notifications.PublishDepartmentHeadReviewRequestedAsync(app, TestDataSeeder.HrUserId);

        (await db.Notifications.CountAsync(n => n.UserId == TestDataSeeder.ManagerUserId)).Should().Be(0);
        (await db.PublishedDomainEvents.AnyAsync(e => e.EventType == WorkflowEventTypes.PassedToHeadReview)).Should().BeTrue();
    }

    // ---- failure / dead-letter / retry (direct row seeding for determinism) ----

    private static async Task<Guid> SeedExecutionAsync(
        AppDbContext db, string actionsJson, WorkflowExecutionStatus status, int attemptCount, WorkflowMode mode)
    {
        var def = new WorkflowDefinition { Id = Guid.NewGuid(), Name = $"wf-{Guid.NewGuid():N}", IsEnabled = true, CreatedAt = DbDateTime.Now };
        var ver = new WorkflowDefinitionVersion
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = def.Id,
            VersionNo = 1,
            TriggerJson = $"{{\"eventType\":\"{WorkflowEventTypes.PassedToHeadReview}\"}}",
            ConditionsJson = "[]",
            ActionsJson = actionsJson,
            Mode = mode,
            IsActive = true,
            PublishedAt = DbDateTime.Now,
            CreatedAt = DbDateTime.Now,
        };
        var exec = new WorkflowExecution
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = def.Id,
            WorkflowDefinitionVersionId = ver.Id,
            Status = status,
            Mode = mode,
            TriggerEventType = WorkflowEventTypes.PassedToHeadReview,
            InputPayloadJson = System.Text.Json.JsonSerializer.Serialize(AppPayload()),
            AttemptCount = attemptCount,
            CreatedAt = DbDateTime.Now,
        };
        db.WorkflowDefinitions.Add(def);
        db.WorkflowDefinitionVersions.Add(ver);
        db.WorkflowExecutions.Add(exec);
        await db.SaveChangesAsync();
        return exec.Id;
    }

    [Fact]
    public async Task Engine_UnknownAction_FailsAndDeadLetters()
    {
        using var factory = await ArrangeAsync(WorkflowMode.Shadow);
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Directly seed an active workflow whose action handler does not exist.
        await SeedExecutionAsync(db, "[{\"type\":\"explode\",\"config\":{}}]", WorkflowExecutionStatus.Success, 0, WorkflowMode.Shadow);
        // Remove the seeded (placeholder) execution row so the engine creates a fresh one.
        db.WorkflowExecutions.RemoveRange(db.WorkflowExecutions);
        await db.SaveChangesAsync();

        Guid eventId = await PublishAndGetEventIdAsync(scope, WorkflowEventTypes.PassedToHeadReview, AppPayload());
        await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().ProcessEventAsync(eventId);

        var execution = await db.WorkflowExecutions.FirstAsync();
        execution.Status.Should().Be(WorkflowExecutionStatus.Failed);
        (await db.WorkflowActionDeadLetters.AnyAsync(d => d.ExecutionId == execution.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Retry_ValidAction_Succeeds()
    {
        using var factory = await ArrangeAsync(WorkflowMode.Shadow);
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Guid execId = await SeedExecutionAsync(
            db,
            "[{\"type\":\"notify_user\",\"config\":{\"recipients\":[\"assignedRecruiter\"]}}]",
            WorkflowExecutionStatus.Failed, attemptCount: 1, WorkflowMode.Shadow);

        var status = await scope.ServiceProvider.GetRequiredService<IWorkflowRetryService>()
            .RetryExecutionAsync(execId, manual: true);

        status.Should().Be(WorkflowExecutionStatus.Success);
    }

    [Fact]
    public async Task Retry_UnknownAction_ExhaustsToDeadLetter()
    {
        using var factory = await ArrangeAsync(WorkflowMode.Shadow);
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Guid execId = await SeedExecutionAsync(
            db, "[{\"type\":\"explode\",\"config\":{}}]",
            WorkflowExecutionStatus.Failed, attemptCount: 3, WorkflowMode.Shadow); // == MaxAttempts

        var status = await scope.ServiceProvider.GetRequiredService<IWorkflowRetryService>()
            .RetryExecutionAsync(execId, manual: true);

        status.Should().Be(WorkflowExecutionStatus.DeadLetter);
    }

    // ============================== Scheduler ==============================

    [Fact]
    public async Task Scheduler_OverdueApplication_PublishesEvent_Once()
    {
        using var factory = await ArrangeAsync(WorkflowMode.Shadow);
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var app = await db.Applications.FirstAsync(a => a.Id == TestDataSeeder.ApplicationId);
        app.DepartmentHeadReviewRequestedAt = DbDateTime.Now.AddDays(-5); // > 3-day threshold
        await db.SaveChangesAsync();

        var scanner = scope.ServiceProvider.GetRequiredService<IHeadReviewOverdueScanner>();
        await scanner.ScanAsync();
        await scanner.ScanAsync(); // second scan same window must not duplicate

        (await db.PublishedDomainEvents.CountAsync(e => e.EventType == WorkflowEventTypes.HeadReviewOverdue))
            .Should().Be(1);
    }

    // ============================== API / RBAC ==============================

    private HttpClient ClientAs(AutomationWebAppFactory factory, Guid userId, params string[] roles)
    {
        var client = factory.CreateClient();
        PostgresTestFixture.SetBearerToken(client, _fixture.CreateJwt(userId.ToString(), roles));
        return client;
    }

    [Fact]
    public async Task Hr_Manager_Candidate_Cannot_Access_Automation_Apis()
    {
        using var factory = await ArrangeAsync();
        foreach (var (userId, role) in new[]
        {
            (TestDataSeeder.HrUserId, "HR"),
            (TestDataSeeder.ManagerUserId, "Manager"),
            (TestDataSeeder.CandidateUserId, "Candidate"),
        })
        {
            var client = ClientAs(factory, userId, role);
            var response = await client.GetAsync("/api/sysadmin/automation/workflows");
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"role {role} must be blocked");
        }
    }

    [Fact]
    public async Task Unauthenticated_Gets_401()
    {
        using var factory = await ArrangeAsync();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/sysadmin/automation/workflows");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SystemAdmin_Can_List_Create_Publish_Enable_Workflow()
    {
        using var factory = await ArrangeAsync();
        var client = ClientAs(factory, TestDataSeeder.SystemAdminUserId, "SystemAdmin");

        (await client.GetAsync("/api/sysadmin/automation/workflows")).StatusCode.Should().Be(HttpStatusCode.OK);

        var create = await client.PostAsJsonAsync("/api/sysadmin/automation/workflows", new
        {
            name = "RBAC Test WF",
            description = "created by test",
            triggerEventType = WorkflowEventTypes.CandidateApplied,
            mode = "Shadow",
            conditions = Array.Empty<object>(),
            actions = new[] { new { type = "notify_user", configJson = "{\"recipients\":[\"assignedRecruiter\"]}" } },
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var defId = await db.WorkflowDefinitions.Where(d => d.Name == "RBAC Test WF").Select(d => d.Id).FirstAsync();

        (await client.PostAsync($"/api/sysadmin/automation/workflows/{defId}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var enable = await client.PatchAsJsonAsync($"/api/sysadmin/automation/workflows/{defId}/enabled", new { isEnabled = false });
        enable.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SystemAdmin_Can_Retry_Failed_Execution_Via_Api()
    {
        using var factory = await ArrangeAsync();
        Guid execId;
        using (var scope = factory.CreateServiceScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            execId = await SeedExecutionAsync(
                db, "[{\"type\":\"notify_user\",\"config\":{\"recipients\":[\"assignedRecruiter\"]}}]",
                WorkflowExecutionStatus.Failed, 1, WorkflowMode.Shadow);
        }

        var client = ClientAs(factory, TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        var response = await client.PostAsync($"/api/sysadmin/automation/executions/{execId}/retry", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ============================== MCP ==============================

    [Fact]
    public async Task SystemAdmin_Can_List_McpTools()
    {
        using var factory = await ArrangeAsync();
        var client = ClientAs(factory, TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        var response = await client.GetAsync("/api/sysadmin/mcp/tools");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain(McpToolNames.JobsSearch);
    }

    [Fact]
    public async Task Mcp_AllowedToolCall_WritesAudit()
    {
        using var factory = await ArrangeAsync();
        var client = ClientAs(factory, TestDataSeeder.SystemAdminUserId, "SystemAdmin");

        var response = await client.PostAsJsonAsync(
            $"/api/sysadmin/mcp/tools/{McpToolNames.JobsSearch}/test", new { inputJson = "{}" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.McpToolAudits.AnyAsync(a => a.ToolName == McpToolNames.JobsSearch && a.Allowed)).Should().BeTrue();
    }

    [Fact]
    public async Task Mcp_DeniedToolCall_WritesAudit()
    {
        using var factory = await ArrangeAsync();
        using var scope = factory.CreateServiceScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mcp = scope.ServiceProvider.GetRequiredService<IMcpToolService>();

        // Non-admin caller: service-level RBAC denies and still audits.
        var result = await mcp.InvokeAsync(McpToolNames.JobsSearch, TestDataSeeder.HrUserId, ["HR"], "{}");
        result.Data!.Allowed.Should().BeFalse();
        (await db.McpToolAudits.AnyAsync(a => a.ToolName == McpToolNames.JobsSearch && !a.Allowed)).Should().BeTrue();
    }

    [Fact]
    public async Task Mcp_HonorsApplicationOwnership()
    {
        using var factory = await ArrangeAsync();
        using var scope = factory.CreateServiceScope();
        var mcp = scope.ServiceProvider.GetRequiredService<IMcpToolService>();

        // SystemAdmin is not an owner of the application, so the underlying service gates the data:
        // the call is allowed + audited, but the summary reflects a non-successful (ownership-gated) read.
        var result = await mcp.InvokeAsync(
            McpToolNames.ApplicationsGet, TestDataSeeder.SystemAdminUserId, ["SystemAdmin"],
            $"{{\"applicationId\":\"{TestDataSeeder.ApplicationId}\"}}");

        result.Data!.Allowed.Should().BeTrue();
        result.Data.Output.Should().NotBeNull();
    }
}
