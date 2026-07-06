using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RecruitPro.Tests.Infrastructure;

namespace RecruitPro.Tests;

/// <summary>
/// Job posting deadline rule: the application deadline must fall AFTER the posting date.
///   • POST /api/hr/jobs with a past/today deadline → 400 JOB_DEADLINE_INVALID
///   • POST with a future deadline → 201 and the deadline persists
///   • PATCH /api/hr/jobs/{id} moving the deadline before CreatedAt → 400
///   • PATCH with a valid deadline → 200
/// </summary>
public sealed class JobDeadlineValidationTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly RecruitProWebApplicationFactory _factory;
    private HttpClient _client = default!;

    public JobDeadlineValidationTests(PostgresTestFixture fixture)
    {
        _factory = new RecruitProWebApplicationFactory(fixture);
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
        string token = _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR");
        PostgresTestFixture.SetBearerToken(_client, token);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync();
    }

    private static object BuildCreatePayload(DateTime? deadline) => new
    {
        title = "Deadline Rule Job",
        departmentId = TestDataSeeder.DepartmentId.ToString(),
        employmentType = "FullTime",
        workMode = "Onsite",
        location = "HCMC",
        description = "Validate the deadline business rule.",
        requirements = new[] { "Any" },
        vacancyCount = 1,
        deadline,
    };

    [Fact]
    public async Task CreateJob_DeadlineInPast_Returns400()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/hr/jobs", BuildCreatePayload(DateTime.Now.AddDays(-1)));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("JOB_DEADLINE_INVALID");
    }

    [Fact]
    public async Task CreateJob_DeadlineToday_Returns400()
    {
        // Same-day deadline is invalid: the rule is strictly AFTER the posting date.
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/hr/jobs", BuildCreatePayload(DateTime.Now.Date.AddHours(23)));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateJob_FutureDeadline_Returns201_AndPersists()
    {
        DateTime deadline = DateTime.Now.Date.AddDays(30);
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/hr/jobs", BuildCreatePayload(deadline));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);
        string jobId = json.RootElement.GetProperty("data").GetProperty("jobId").GetString()!;

        HttpResponseMessage detailResponse = await _client.GetAsync($"/api/hr/jobs/{jobId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonDocument detailJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(detailResponse);
        string? persisted = detailJson.RootElement.GetProperty("data").GetProperty("deadline").GetString();
        persisted.Should().NotBeNull();
        DateTime.Parse(persisted!).Date.Should().Be(deadline.Date);
    }

    [Fact]
    public async Task CreateJob_NoDeadline_StillAllowed()
    {
        // Open-until-filled jobs stay legal — the rule only fires when a deadline is provided.
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/hr/jobs", BuildCreatePayload(null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PatchJob_DeadlineBeforeCreatedAt_Returns400()
    {
        // Seeded approved job was created 10 days ago; a deadline before that is invalid.
        HttpResponseMessage response = await _client.PatchAsJsonAsync(
            $"/api/hr/jobs/{TestDataSeeder.ApprovedJobId}",
            new { deadline = DateTime.Now.AddDays(-30) });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("JOB_DEADLINE_INVALID");
    }

    [Fact]
    public async Task PatchJob_ValidDeadline_Returns200()
    {
        HttpResponseMessage response = await _client.PatchAsJsonAsync(
            $"/api/hr/jobs/{TestDataSeeder.ApprovedJobId}",
            new { deadline = DateTime.Now.AddDays(45) });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
