using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Infrastructure.Data;
using RecruitPro.Tests.Infrastructure;

namespace RecruitPro.Tests;

/// <summary>
/// Phase 4 hardening — the job approval queue/detail are now the DepartmentHead's workflow surface
/// (BR-OWN-003). Access is scoped to the job's Department.HeadUserId; SystemAdmin-only and a generic
/// Manager who is not the department head no longer see or approve other departments' jobs. The route names
/// (`/api/manager/...`) are kept for compatibility. See TEST-MATRIX.md (T-OWN-034..040).
/// </summary>
public sealed class ApprovalQueueAccessIntegrationTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly RecruitProWebApplicationFactory _factory;
    private HttpClient _client = default!;

    public ApprovalQueueAccessIntegrationTests(PostgresTestFixture fixture)
    {
        _factory = new RecruitProWebApplicationFactory(fixture);
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync();
    }

    private const string QueuePath = "/api/manager/jobs/approval-queue";
    private static readonly string DetailPath = $"/api/manager/jobs/{TestDataSeeder.PendingJobId}/approval-detail";
    private static readonly string StatusPath = $"/api/jobs/{TestDataSeeder.PendingJobId}/status";

    // Reassign the seeded Engineering department head (Manager User by default) to a different user.
    private async Task SetEngineeringHeadAsync(Guid? headUserId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var department = await db.Departments.SingleAsync(d => d.Id == TestDataSeeder.DepartmentId);
        department.HeadUserId = headUserId;
        await db.SaveChangesAsync();
    }

    private static bool QueueContainsPendingJob(JsonDocument json)
    {
        return json.RootElement.GetProperty("data").GetProperty("items")
            .EnumerateArray()
            .Any(item => item.GetProperty("jobId").GetString() == TestDataSeeder.PendingJobId.ToString());
    }

    // T-OWN-034: a DepartmentHead sees the approval queue for the department they head.
    [Fact]
    public async Task HeadDepartment_CanViewOwnDepartmentApprovalQueue()
    {
        await SetEngineeringHeadAsync(TestDataSeeder.HeadDepartmentUserId);
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HeadDepartmentUserId.ToString(), "HeadDepartment"));

        HttpResponseMessage response = await _client.GetAsync(QueuePath);
        using JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        QueueContainsPendingJob(json).Should().BeTrue();
    }

    // T-OWN-035: a DepartmentHead does NOT see jobs of departments they do not head. In the seed the
    // Engineering head is the Manager user, so the HeadDepartment user (who heads nothing) gets an empty
    // queue rather than another department's jobs.
    [Fact]
    public async Task HeadDepartment_CannotViewOtherDepartmentApprovalQueue()
    {
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HeadDepartmentUserId.ToString(), "HeadDepartment"));

        HttpResponseMessage response = await _client.GetAsync(QueuePath);
        using JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        QueueContainsPendingJob(json).Should().BeFalse();
        json.RootElement.GetProperty("data").GetProperty("summary").GetProperty("pendingApprovals").GetInt32().Should().Be(0);
    }

    // T-OWN-036: SystemAdmin-only is not a recruitment workflow role for the approval queue.
    [Fact]
    public async Task SystemAdminOnly_CannotViewApprovalQueue()
    {
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.SystemAdminUserId.ToString(), "SystemAdmin"));

        HttpResponseMessage response = await _client.GetAsync(QueuePath);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-OWN-037: a DepartmentHead can open the approval detail for a job in the department they head.
    [Fact]
    public async Task HeadDepartment_CanViewOwnApprovalDetail()
    {
        await SetEngineeringHeadAsync(TestDataSeeder.HeadDepartmentUserId);
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HeadDepartmentUserId.ToString(), "HeadDepartment"));

        HttpResponseMessage response = await _client.GetAsync(DetailPath);
        using JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("data").GetProperty("jobId").GetString().Should().Be(TestDataSeeder.PendingJobId.ToString());
    }

    // T-OWN-038: a DepartmentHead cannot open the approval detail of another department's job → 403.
    [Fact]
    public async Task HeadDepartment_CannotViewOtherDepartmentApprovalDetail()
    {
        // Engineering head stays the Manager user; the HeadDepartment user heads nothing.
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HeadDepartmentUserId.ToString(), "HeadDepartment"));

        HttpResponseMessage response = await _client.GetAsync(DetailPath);
        using JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");
    }

    // T-OWN-039: a Manager who IS the department head keeps full access (compatibility) — queue + detail.
    [Fact]
    public async Task Manager_WhoIsDepartmentHead_CanViewQueueAndDetail()
    {
        // Seed default: the Manager user is the Engineering department head.
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.ManagerUserId.ToString(), "Manager"));

        HttpResponseMessage queue = await _client.GetAsync(QueuePath);
        using JsonDocument queueJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(queue);
        queue.StatusCode.Should().Be(HttpStatusCode.OK);
        QueueContainsPendingJob(queueJson).Should().BeTrue();

        HttpResponseMessage detail = await _client.GetAsync(DetailPath);
        using JsonDocument detailJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(detail);
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        detailJson.RootElement.GetProperty("data").GetProperty("jobId").GetString().Should().Be(TestDataSeeder.PendingJobId.ToString());
    }

    // T-OWN-040: a legacy Manager who is NOT the department head cannot view OR approve another
    // department's job. The queue is empty, the detail is 403, and the approve submit is 403.
    [Fact]
    public async Task LegacyManager_NotDepartmentHead_CannotApproveOrViewOtherDepartmentJob()
    {
        // Move the Engineering head to the HeadDepartment user so the Manager user is no longer the head.
        await SetEngineeringHeadAsync(TestDataSeeder.HeadDepartmentUserId);
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.ManagerUserId.ToString(), "Manager"));

        HttpResponseMessage queue = await _client.GetAsync(QueuePath);
        using JsonDocument queueJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(queue);
        queue.StatusCode.Should().Be(HttpStatusCode.OK);
        QueueContainsPendingJob(queueJson).Should().BeFalse();

        HttpResponseMessage detail = await _client.GetAsync(DetailPath);
        using JsonDocument detailJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(detail);
        detail.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        detailJson.RootElement.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");

        HttpResponseMessage submit = await _client.PatchAsJsonAsync(StatusPath, new { status = "Approved" });
        using JsonDocument submitJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(submit);
        submit.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        submitJson.RootElement.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");
    }
}
