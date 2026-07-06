using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RecruitPro.Tests.Infrastructure;

namespace RecruitPro.Tests;

/// <summary>
/// System Admin console — RBAC matrix, user directory, and audit logs.
/// Rules verified:
///   • Endpoints are permission-guarded ([RequirePermission]) against the live role_permissions rows,
///     not a hard-coded role: users lacking the permission get 403, holders get 200.
///   • PUT matrix validates: unknown code → 400, unknown role → 404, admin-lockout → 409.
///   • Matrix changes apply immediately (no re-login): revoking HR's Job_CREATE has no bearing here,
///     but granting HR PERMISSION_VIEW lets HR read the matrix on the next request.
///   • User directory: status change guards (self-deactivation 409, last-admin 409).
/// </summary>
public sealed class SysAdminRbacTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly RecruitProWebApplicationFactory _factory;
    private HttpClient _client = default!;

    public SysAdminRbacTests(PostgresTestFixture fixture)
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

    // ---- read access ----

    [Fact]
    public async Task Rbac_Roles_Anonymous_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage response = await _client.GetAsync("/api/sysadmin/rbac/roles");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rbac_Roles_Candidate_Returns403()
    {
        Authenticate(TestDataSeeder.CandidateUserId, "Candidate");
        HttpResponseMessage response = await _client.GetAsync("/api/sysadmin/rbac/roles");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Rbac_Roles_SystemAdmin_Returns200_WithSeededRoles()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync("/api/sysadmin/rbac/roles");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);
        var roleNames = json.RootElement.GetProperty("data").EnumerateArray()
            .Select(role => role.GetProperty("name").GetString())
            .ToList();
        roleNames.Should().Contain(["SystemAdmin", "HR", "Candidate", "Manager", "HeadDepartment"]);
    }

    [Fact]
    public async Task Rbac_Modules_SystemAdmin_Returns200_WithCatalog()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync("/api/sysadmin/rbac/modules");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);
        var moduleKeys = json.RootElement.GetProperty("data").EnumerateArray()
            .Select(module => module.GetProperty("key").GetString())
            .ToList();
        moduleKeys.Should().Contain(["jobs", "applications", "users", "roles", "rbac"]);
    }

    [Fact]
    public async Task Rbac_RolePermissions_UnknownRole_Returns404()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync($"/api/sysadmin/rbac/roles/{Guid.NewGuid()}/permissions");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- update matrix ----

    [Fact]
    public async Task Rbac_UpdatePermissions_Hr_Returns403()
    {
        Authenticate(TestDataSeeder.HrUserId, "HR");
        HttpResponseMessage response = await _client.PutAsJsonAsync(
            $"/api/sysadmin/rbac/roles/{TestDataSeeder.CandidateRoleId}/permissions",
            new { permissionCodes = new[] { "Job_VIEW" } });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rbac_UpdatePermissions_UnknownCode_Returns400()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.PutAsJsonAsync(
            $"/api/sysadmin/rbac/roles/{TestDataSeeder.CandidateRoleId}/permissions",
            new { permissionCodes = new[] { "Job_VIEW", "TOTALLY_FAKE_PERMISSION" } });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("RBAC_UNKNOWN_PERMISSION");
    }

    [Fact]
    public async Task Rbac_UpdatePermissions_UnknownRole_Returns404()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.PutAsJsonAsync(
            $"/api/sysadmin/rbac/roles/{Guid.NewGuid()}/permissions",
            new { permissionCodes = new[] { "Job_VIEW" } });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rbac_UpdatePermissions_Valid_Returns200_AndPersists()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        string[] newSet = ["Job_VIEW", "Application_VIEW", "NOTIFICATION_VIEW"];

        HttpResponseMessage putResponse = await _client.PutAsJsonAsync(
            $"/api/sysadmin/rbac/roles/{TestDataSeeder.HeadDepartmentRoleId}/permissions",
            new { permissionCodes = newSet });
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage getResponse = await _client.GetAsync(
            $"/api/sysadmin/rbac/roles/{TestDataSeeder.HeadDepartmentRoleId}/permissions");
        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(getResponse);
        var granted = json.RootElement.GetProperty("data").GetProperty("grantedCodes").EnumerateArray()
            .Select(code => code.GetString())
            .ToList();
        granted.Should().BeEquivalentTo(newSet);
    }

    [Fact]
    public async Task Rbac_RemovingManageFromLastAdminRole_Returns409()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");

        // SystemAdmin is the only role granting PERMISSION_MANAGE and the admin user is its only
        // member — stripping PERMISSION_MANAGE would lock the whole system out of RBAC management.
        HttpResponseMessage response = await _client.PutAsJsonAsync(
            $"/api/sysadmin/rbac/roles/{TestDataSeeder.SystemAdminRoleId}/permissions",
            new { permissionCodes = new[] { "PERMISSION_VIEW", "ROLE_VIEW", "USER_VIEW" } });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("RBAC_ADMIN_LOCKOUT");
    }

    [Fact]
    public async Task Rbac_GrantingPermissionView_TakesEffectWithoutRelogin()
    {
        // HR cannot read the matrix initially (no PERMISSION_VIEW in the seed).
        Authenticate(TestDataSeeder.HrUserId, "HR");
        HttpResponseMessage before = await _client.GetAsync("/api/sysadmin/rbac/modules");
        before.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // SystemAdmin grants HR its current set + PERMISSION_VIEW + ROLE_VIEW.
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        string[] hrSet =
        [
            "Job_VIEW", "Job_CREATE", "Job_UPDATE", "Application_VIEW", "Application_REVIEW",
            "Interview_VIEW", "Interview_CREATE", "Interview_UPDATE",
            "CANDIDATE_PROFILE_VIEW", "DEPARTMENT_VIEW", "SKILL_VIEW", "NOTIFICATION_VIEW",
            "PERMISSION_VIEW", "ROLE_VIEW",
        ];
        HttpResponseMessage grant = await _client.PutAsJsonAsync(
            $"/api/sysadmin/rbac/roles/{TestDataSeeder.HrRoleId}/permissions",
            new { permissionCodes = hrSet });
        grant.StatusCode.Should().Be(HttpStatusCode.OK);

        // Same HR token now passes the DB-backed permission check — no new JWT needed.
        Authenticate(TestDataSeeder.HrUserId, "HR");
        HttpResponseMessage after = await _client.GetAsync("/api/sysadmin/rbac/modules");
        after.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- user directory ----

    [Fact]
    public async Task Users_List_SystemAdmin_Returns200_AndFiltersByRole()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/sysadmin/users?roleId={TestDataSeeder.HrRoleId}&page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);
        var items = json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(1);
        items[0].GetProperty("email").GetString().Should().Be("hr@recruitpro.test");
    }

    [Fact]
    public async Task Users_List_Candidate_Returns403()
    {
        Authenticate(TestDataSeeder.CandidateUserId, "Candidate");
        HttpResponseMessage response = await _client.GetAsync("/api/sysadmin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Users_DeactivateSelf_Returns409()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.PatchAsJsonAsync(
            $"/api/sysadmin/users/{TestDataSeeder.SystemAdminUserId}/status",
            new { status = "Inactive" });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Users_Deactivate_InvalidStatus_Returns400()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.PatchAsJsonAsync(
            $"/api/sysadmin/users/{TestDataSeeder.CandidateUserId}/status",
            new { status = "SuperBanned" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Users_DeactivateCandidate_Returns200_AndBlocksLogin()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage patchResponse = await _client.PatchAsJsonAsync(
            $"/api/sysadmin/users/{TestDataSeeder.CandidateUserId}/status",
            new { status = "Inactive" });
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // A deactivated account can no longer authenticate.
        _client.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/candidate/login",
            new { username = "candidate.user", password = "Pass@123" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Users_RemoveAdminRoleFromLastAdmin_Returns409()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.PutAsJsonAsync(
            $"/api/sysadmin/users/{TestDataSeeder.SystemAdminUserId}/roles",
            new { roleIds = new[] { TestDataSeeder.CandidateRoleId.ToString() } });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("RBAC_ADMIN_LOCKOUT");
    }

    // ---- audit logs + overview ----

    [Fact]
    public async Task AuditLogs_SystemAdmin_Returns200_AndRecordsRbacChanges()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        await _client.PutAsJsonAsync(
            $"/api/sysadmin/rbac/roles/{TestDataSeeder.CandidateRoleId}/permissions",
            new { permissionCodes = new[] { "Job_VIEW", "Application_APPLY" } });

        HttpResponseMessage response = await _client.GetAsync("/api/sysadmin/audit-logs?q=RBAC");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);
        var actions = json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("action").GetString())
            .ToList();
        actions.Should().Contain("RBAC_PERMISSIONS_UPDATED");
    }

    [Fact]
    public async Task AuditLogs_Candidate_Returns403()
    {
        Authenticate(TestDataSeeder.CandidateUserId, "Candidate");
        HttpResponseMessage response = await _client.GetAsync("/api/sysadmin/audit-logs");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Overview_SystemAdmin_Returns200_WithCounts()
    {
        Authenticate(TestDataSeeder.SystemAdminUserId, "SystemAdmin");
        HttpResponseMessage response = await _client.GetAsync("/api/sysadmin/overview");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);
        JsonElement data = json.RootElement.GetProperty("data");
        data.GetProperty("totalUsers").GetInt32().Should().BeGreaterThan(0);
        data.GetProperty("totalRoles").GetInt32().Should().Be(5);
        data.GetProperty("roles").EnumerateArray().Should().NotBeEmpty();
    }

    private void Authenticate(Guid userId, params string[] roles)
    {
        string token = _factory.Fixture.CreateJwt(userId.ToString(), roles);
        PostgresTestFixture.SetBearerToken(_client, token);
    }
}
