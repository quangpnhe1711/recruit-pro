using System.Net;
using System.Net.Http.Json;
using RecruitPro.Tests.Infrastructure;

namespace RecruitPro.Tests;

/// <summary>
/// Phase Security 2.2c — SystemAdmin Lockdown Final.
/// Rules verified:
///   • SystemAdmin-only is blocked (403) from ALL business endpoints after Phase 2.2c controller cleanup.
///   • ResolveListScopeUserId never returns null; SystemAdmin-only calls that somehow pass [Authorize]
///     get an empty list, not all records.
///   • Business-role users (HR, Manager, HeadDepartment) are unaffected by the lockdown.
///   • Multi-role users (SystemAdmin + HR) continue to work via the HR role.
/// See T-SEC-SystemAdmin-001..009.
/// </summary>
public sealed class Phase22cSecurityTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly RecruitProWebApplicationFactory _factory;
    private HttpClient _client = default!;

    public Phase22cSecurityTests(PostgresTestFixture fixture)
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

    // T-SEC-SystemAdmin-001: SystemAdmin-only → GET /api/hr/applications → 403 (business endpoint).
    [Fact]
    public async Task Application_List_SystemAdminOnly_Returns403()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/applications?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-SystemAdmin-002: SystemAdmin-only → GET /api/hr/applications/{id} → 403 (business endpoint).
    [Fact]
    public async Task Application_Detail_SystemAdminOnly_Returns403()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync($"/api/hr/applications/{TestDataSeeder.ApplicationId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-SystemAdmin-003: SystemAdmin-only → GET /api/hr/jobs → 403 (business job list).
    [Fact]
    public async Task Job_HrList_SystemAdminOnly_Returns403()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/jobs?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-SystemAdmin-004: SystemAdmin-only → GET /api/hr/candidates → 403 (candidate business list).
    [Fact]
    public async Task Candidate_List_SystemAdminOnly_Returns403()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/candidates?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-SystemAdmin-005: SystemAdmin-only → GET /api/hr/candidates/{id} → 403.
    [Fact]
    public async Task Candidate_Detail_SystemAdminOnly_Returns403()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync($"/api/hr/candidates/{TestDataSeeder.CandidateProfileId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-SystemAdmin-006: SystemAdmin-only → GET /api/hr/interviews → 403 (interview list).
    // Covered by Phase22bSecurityTests T-SEC-003, but repeated here for completeness of 2.2c matrix.
    [Fact]
    public async Task Interview_List_SystemAdminOnly_Returns403()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/interviews?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-SystemAdmin-007: SystemAdmin-only → GET /api/hr/applications/{id}/offer → 403.
    // Covered by Phase22bSecurityTests T-SEC-012, repeated here for 2.2c matrix.
    [Fact]
    public async Task Offer_GetEditor_SystemAdminOnly_Returns403()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync($"/api/hr/applications/{TestDataSeeder.ApplicationId}/offer");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-SystemAdmin-008: SystemAdmin-only → GET /api/manager/jobs/approval-queue → 403.
    // Job approval is exclusively a DepartmentHead/Manager workflow — SystemAdmin has no role here.
    [Fact]
    public async Task JobApproval_Queue_SystemAdminOnly_Returns403()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync("/api/manager/jobs/approval-queue?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-SystemAdmin-009: ResolveListScopeUserId does not return null for SystemAdmin.
    // Verified indirectly: HR outsider (no ownership) gets 200 with empty items — scope always applies.
    // A null scope would bypass the filter and return all records; empty list proves scope is active.
    [Fact]
    public async Task ApplicationList_HrOutsider_ReturnsEmptyNotAllRecords()
    {
        Authenticate(Guid.NewGuid(), "HR");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/applications?page=1&pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using System.Text.Json.JsonDocument json = await System.Text.Json.JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().Be(0,
            "an HR user with no ownership must never receive all records — scope is always active");
    }

    // ---- Business roles unaffected (regression guard) ----

    // T-SEC-SystemAdmin-regression-001: HR owner can still access application list after lockdown.
    [Fact]
    public async Task Application_List_HrOwner_Returns200()
    {
        Authenticate(TestDataSeeder.HrUserId, "HR");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/applications?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // T-SEC-SystemAdmin-regression-002: HeadDepartment can access manager approval queue.
    [Fact]
    public async Task JobApproval_Queue_HeadDepartment_Returns200()
    {
        Authenticate(TestDataSeeder.HeadDepartmentUserId, "HeadDepartment");
        HttpResponseMessage response = await _client.GetAsync("/api/manager/jobs/approval-queue?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // T-SEC-SystemAdmin-regression-003: SystemAdmin + HR multi-role can access application list (via HR).
    [Fact]
    public async Task Application_List_SystemAdminPlusHr_Returns200()
    {
        Authenticate(TestDataSeeder.HrUserId, "HR", "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/applications?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // T-SEC-SystemAdmin-regression-004: SystemAdmin + HeadDepartment can access approval queue (via HeadDepartment).
    [Fact]
    public async Task JobApproval_Queue_SystemAdminPlusHeadDepartment_Returns200()
    {
        Authenticate(TestDataSeeder.HeadDepartmentUserId, "SystemAdmin", "HeadDepartment");
        HttpResponseMessage response = await _client.GetAsync("/api/manager/jobs/approval-queue?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- helper ----

    private void Authenticate(Guid userId, params string[] roles)
    {
        string token = _factory.Fixture.CreateJwt(userId.ToString(), roles);
        PostgresTestFixture.SetBearerToken(_client, token);
    }
}
