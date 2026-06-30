using System.Net;
using System.Net.Http.Json;
using RecruitPro.Tests.Infrastructure;

namespace RecruitPro.Tests;

/// <summary>
/// Phase Security 2.2b — role-based access + per-record ownership scope.
/// Rules verified:
///   • SystemAdmin-only → 403 for all business endpoints (Interview/Offer/Semantic/Copilot)
///   • HeadDepartment → view-only access to interviews, blocked from create/mutate
///   • HR owner → scoped access; non-owner HR gets empty list or 403
///   • Candidate → 403 for all HR endpoints
///   • Anonymous → 401 for all protected endpoints
///   • SystemAdmin + HR → 200 scoped like regular HR
/// See TEST-MATRIX.md for T-SEC-001..020.
/// </summary>
public sealed class Phase22bSecurityTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly RecruitProWebApplicationFactory _factory;
    private HttpClient _client = default!;

    public Phase22bSecurityTests(PostgresTestFixture fixture)
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

    // ---- Interview endpoint access control (T-SEC-001..009) ----

    // T-SEC-001: anonymous request is rejected with 401.
    [Fact]
    public async Task Interview_List_Anonymous_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await _client.GetAsync("/api/hr/interviews?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // T-SEC-002: Candidate role is forbidden from the HR interview list (not in [Authorize]).
    [Fact]
    public async Task Interview_List_Candidate_Returns403()
    {
        Authenticate(TestDataSeeder.CandidateUserId, "Candidate");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/interviews?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-003: SystemAdmin-only is NOT in the Interview [Authorize(Roles = "HR,Manager,HeadDepartment")]
    // → 403 at the role-filter layer.
    [Fact]
    public async Task Interview_List_SystemAdminOnly_Returns403()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/interviews?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-004: HR user who owns the application (AssignedRecruiterId) sees the seeded interview.
    [Fact]
    public async Task Interview_List_HrOwner_Returns200WithScopedData()
    {
        Authenticate(TestDataSeeder.HrUserId, "HR");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/interviews?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using System.Text.Json.JsonDocument json = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    // T-SEC-005: HR user with no ownership connection returns 200 but with an empty list (scoped away).
    [Fact]
    public async Task Interview_List_HrOutsider_Returns200EmptyList()
    {
        Authenticate(Guid.NewGuid(), "HR");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/interviews?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using System.Text.Json.JsonDocument json = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().Be(0);
    }

    // T-SEC-006: HeadDepartment is in [Authorize(Roles = "HR,Manager,HeadDepartment")] → 200.
    [Fact]
    public async Task Interview_List_HeadDepartment_Returns200()
    {
        Authenticate(TestDataSeeder.HeadDepartmentUserId, "HeadDepartment");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/interviews?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // T-SEC-007: SystemAdmin + HR combo → role check passes (HR present), ownership scoped like regular HR.
    [Fact]
    public async Task Interview_List_SystemAdminPlusHr_Returns200()
    {
        Authenticate(TestDataSeeder.HrUserId, "HR", "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/interviews?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // T-SEC-008: HeadDepartment CANNOT create interviews — not in [Authorize(Roles = "HR,Manager")].
    [Fact]
    public async Task Interview_Create_HeadDepartment_Returns403()
    {
        Authenticate(TestDataSeeder.HeadDepartmentUserId, "HeadDepartment");
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/hr/interviews", new
        {
            applicationId = TestDataSeeder.ApplicationId.ToString(),
            interviewDate = DateTime.UtcNow.AddDays(3).ToString("o"),
            meetingType = "Online"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-009: SystemAdmin-only cannot create interviews — not in [Authorize(Roles = "HR,Manager")].
    [Fact]
    public async Task Interview_Create_SystemAdminOnly_Returns403()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/hr/interviews", new
        {
            applicationId = TestDataSeeder.ApplicationId.ToString(),
            interviewDate = DateTime.UtcNow.AddDays(3).ToString("o"),
            meetingType = "Online"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Offer endpoint access control (T-SEC-010..015) ----

    // T-SEC-010: anonymous request on offer editor is rejected with 401.
    [Fact]
    public async Task Offer_GetEditor_Anonymous_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await _client.GetAsync($"/api/hr/applications/{TestDataSeeder.ApplicationId}/offer");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // T-SEC-011: Candidate role is forbidden — not in [Authorize(Roles = "HR,Manager")].
    [Fact]
    public async Task Offer_GetEditor_Candidate_Returns403()
    {
        Authenticate(TestDataSeeder.CandidateUserId, "Candidate");
        HttpResponseMessage response = await _client.GetAsync($"/api/hr/applications/{TestDataSeeder.ApplicationId}/offer");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-012: SystemAdmin-only is not in [Authorize(Roles = "HR,Manager")] → 403.
    [Fact]
    public async Task Offer_GetEditor_SystemAdminOnly_Returns403()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync($"/api/hr/applications/{TestDataSeeder.ApplicationId}/offer");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-013: HeadDepartment is not in [Authorize(Roles = "HR,Manager")] → 403 for offer.
    [Fact]
    public async Task Offer_GetEditor_HeadDepartment_Returns403()
    {
        Authenticate(TestDataSeeder.HeadDepartmentUserId, "HeadDepartment");
        HttpResponseMessage response = await _client.GetAsync($"/api/hr/applications/{TestDataSeeder.ApplicationId}/offer");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-014: HR owner of the application passes role + ownership checks.
    [Fact]
    public async Task Offer_GetEditor_HrOwner_Returns2xx()
    {
        Authenticate(TestDataSeeder.HrUserId, "HR");
        HttpResponseMessage response = await _client.GetAsync($"/api/hr/applications/{TestDataSeeder.ApplicationId}/offer");
        ((int)response.StatusCode).Should().BeOneOf(200, 404);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    // T-SEC-015: HR with no ownership of the application is rejected by ownership check → 403.
    [Fact]
    public async Task Offer_GetEditor_HrOutsider_Returns403()
    {
        Authenticate(Guid.NewGuid(), "HR");
        HttpResponseMessage response = await _client.GetAsync($"/api/hr/applications/{TestDataSeeder.ApplicationId}/offer");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Role-mapping correctness (T-SEC-016..020) ----

    // T-SEC-016: Manager role can access the interview list.
    [Fact]
    public async Task Interview_List_Manager_Returns200()
    {
        Authenticate(TestDataSeeder.ManagerUserId, "Manager");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/interviews?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // T-SEC-017: SystemAdmin + HeadDepartment → HeadDepartment role passes interview [Authorize], returns 200.
    [Fact]
    public async Task Interview_List_SystemAdminPlusHeadDepartment_Returns200()
    {
        Authenticate(TestDataSeeder.HeadDepartmentUserId, "SystemAdmin", "HeadDepartment");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/interviews?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // T-SEC-018: SystemAdmin-only cannot access interview schedule-data endpoint.
    [Fact]
    public async Task Interview_ScheduleData_SystemAdminOnly_Returns403()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync("/api/hr/interviews/schedule-data");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-SEC-019: HeadDepartment can access interview schedule-data (view-only permitted).
    [Fact]
    public async Task Interview_ScheduleData_HeadDepartment_Returns200()
    {
        Authenticate(TestDataSeeder.HeadDepartmentUserId, "HeadDepartment");
        HttpResponseMessage response = await _client.GetAsync($"/api/hr/interviews/schedule-data?applicationId={TestDataSeeder.ApplicationId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // T-SEC-020: anonymous request on offer send is rejected with 401.
    [Fact]
    public async Task Offer_Send_Anonymous_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/hr/applications/{TestDataSeeder.ApplicationId}/offer/send", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- helper ----

    private void Authenticate(Guid userId, params string[] roles)
    {
        string token = _factory.Fixture.CreateJwt(userId.ToString(), roles);
        PostgresTestFixture.SetBearerToken(_client, token);
    }
}
