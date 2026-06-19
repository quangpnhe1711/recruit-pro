using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RecruitPro.Tests.Infrastructure;

namespace RecruitPro.Tests;

public sealed class ControllerIntegrationExpandedTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly RecruitProWebApplicationFactory _factory;
    private HttpClient _client = default!;

    public ControllerIntegrationExpandedTests(PostgresTestFixture fixture)
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

    [Fact]
    public async Task DashboardController_HrAndManagerEndpoints_ReturnExpectedPayloads()
    {
        string hrToken = _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR");
        PostgresTestFixture.SetBearerToken(_client, hrToken);

        HttpResponseMessage hrResponse = await _client.GetAsync("/api/hr/dashboard");
        using JsonDocument hrJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(hrResponse);

        hrResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        hrJson.RootElement.GetProperty("data").GetProperty("stats").GetProperty("activePostings").GetInt32().Should().BeGreaterThan(0);

        string managerToken = _factory.Fixture.CreateJwt(TestDataSeeder.ManagerUserId.ToString(), "Manager");
        PostgresTestFixture.SetBearerToken(_client, managerToken);

        HttpResponseMessage managerResponse = await _client.GetAsync("/api/manager/dashboard");
        using JsonDocument managerJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(managerResponse);

        managerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        managerJson.RootElement.GetProperty("data").GetProperty("summary").GetProperty("pendingApprovals").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task CandidateController_HrCandidateDetail_ReturnsHistory()
    {
        string hrToken = _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR");
        PostgresTestFixture.SetBearerToken(_client, hrToken);

        HttpResponseMessage response = await _client.GetAsync($"/api/hr/candidates/{TestDataSeeder.CandidateProfileId}");
        using JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("data").GetProperty("profile").GetProperty("name").GetString().Should().Be("Candidate User");
        json.RootElement.GetProperty("data").GetProperty("applicationHistory").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task JobController_ManagerApprovalQueue_ReturnsPendingApprovalJob()
    {
        string managerToken = _factory.Fixture.CreateJwt(TestDataSeeder.ManagerUserId.ToString(), "Manager");
        PostgresTestFixture.SetBearerToken(_client, managerToken);

        HttpResponseMessage response = await _client.GetAsync("/api/manager/jobs/approval-queue?page=1&pageSize=10");
        using JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task JobController_HrCreatePatchDeleteJob_ManagesLifecycle()
    {
        string hrToken = _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR");
        PostgresTestFixture.SetBearerToken(_client, hrToken);

        HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/hr/jobs", new
        {
            title = "Platform QA",
            departmentId = TestDataSeeder.DepartmentId.ToString(),
            location = "HCMC",
            description = "Build QA platform",
            requirements = new[] { "Testing" },
            benefits = new[] { "Allowance" },
            vacancyCount = 1,
            skillIds = new[] { TestDataSeeder.SqlSkillId.ToString() }
        });

        using JsonDocument createJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(createResponse);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        string jobId = createJson.RootElement.GetProperty("data").GetProperty("jobId").GetString()!;

        HttpResponseMessage patchResponse = await _client.PatchAsJsonAsync($"/api/hr/jobs/{jobId}", new
        {
            title = "Platform QA Senior",
            approvalStatus = "Approved"
        });

        using JsonDocument patchJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(patchResponse);
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        patchJson.RootElement.GetProperty("data").GetProperty("approvalStatus").GetString().Should().Be("Approved");

        HttpResponseMessage deleteResponse = await _client.DeleteAsync($"/api/hr/jobs/{jobId}");
        using JsonDocument deleteJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(deleteResponse);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        deleteJson.RootElement.GetProperty("message").GetString().Should().Be("Job deleted successfully");
    }
}
