using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Infrastructure.Data;
using RecruitPro.Tests.Infrastructure;

namespace RecruitPro.Tests;

/// <summary>
/// Phase 2/3 hardening — the previously-public `PATCH /api/jobs/{id}/status` endpoint now requires auth
/// and routes through the same DepartmentHead approval guard as `/api/hr/jobs/{id}/status` (BR-OWN-003).
/// Also locks the `/api/departments` lookup route (moved to DepartmentController, path unchanged).
/// See TEST-MATRIX.md (T-OWN-028..033).
/// </summary>
public sealed class JobStatusGuardIntegrationTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly RecruitProWebApplicationFactory _factory;
    private HttpClient _client = default!;

    public JobStatusGuardIntegrationTests(PostgresTestFixture fixture)
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

    private static readonly string StatusPath = $"/api/jobs/{TestDataSeeder.PendingJobId}/status";

    // T-OWN-028: the endpoint now requires authentication.
    [Fact]
    public async Task UpdateJobStatus_WithoutAuth_Returns401()
    {
        HttpResponseMessage response = await _client.PatchAsJsonAsync(StatusPath, new { status = "Approved" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // T-OWN-029: a non-head (HR) cannot approve via this endpoint.
    [Fact]
    public async Task UpdateJobStatus_ApproveByNonHead_Returns403()
    {
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));

        HttpResponseMessage response = await _client.PatchAsJsonAsync(StatusPath, new { status = "Approved" });
        using JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");
    }

    // T-OWN-030: the department head can approve via this endpoint.
    [Fact]
    public async Task UpdateJobStatus_ApproveByDepartmentHead_Succeeds()
    {
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.ManagerUserId.ToString(), "Manager"));

        HttpResponseMessage response = await _client.PatchAsJsonAsync(StatusPath, new { status = "Approved" });
        using JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("data").GetProperty("approvalStatus").GetString().Should().Be("Approved");
    }

    // T-OWN-031: SystemAdmin-only is not a recruitment workflow role on this endpoint.
    [Fact]
    public async Task UpdateJobStatus_ApproveBySystemAdminOnly_Returns403()
    {
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.SystemAdminUserId.ToString(), "SystemAdmin"));

        HttpResponseMessage response = await _client.PatchAsJsonAsync(StatusPath, new { status = "Approved" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T-OWN-032: when the department has no head, approval is a 422 business state.
    [Fact]
    public async Task UpdateJobStatus_WhenDepartmentHasNoHead_Returns422()
    {
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var department = await db.Departments.SingleAsync(d => d.Id == TestDataSeeder.DepartmentId);
            department.HeadUserId = null;
            await db.SaveChangesAsync();
        }

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.ManagerUserId.ToString(), "Manager"));

        HttpResponseMessage response = await _client.PatchAsJsonAsync(StatusPath, new { status = "Approved" });
        using JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be((HttpStatusCode)422);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("DEPARTMENT_HEAD_REQUIRED");
    }

    // Compatibility: GET /api/departments (moved to DepartmentController, path unchanged) still works and
    // now carries the department head.
    [Fact]
    public async Task Departments_LookupRoute_StillReturnsDepartmentsWithHead()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/departments");
        using JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement items = json.RootElement.GetProperty("data");
        items.GetArrayLength().Should().BeGreaterThan(0);
        items.EnumerateArray().Should().Contain(item =>
            item.GetProperty("name").GetString() == "Engineering"
            && item.GetProperty("headUserName").GetString() == "Manager User");
    }
}
