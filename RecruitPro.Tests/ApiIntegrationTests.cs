using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RecruitPro.Tests.Infrastructure;

namespace RecruitPro.Tests;

public sealed class ApiIntegrationTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly RecruitProWebApplicationFactory _factory;
    private HttpClient _client = default!;

    public ApiIntegrationTests(PostgresTestFixture fixture)
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
    public async Task AuthController_CandidateLogin_Should_Return_200_With_Envelope_And_No500()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/candidate/login", new
        {
            username = "candidate.user",
            password = "Pass@123"
        });

        var json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("data").GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AuthController_InternalLogin_InvalidPassword_Should_Return_Controlled_401()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/internal/login", new
        {
            employeeIdOrEmail = "hr@recruitpro.test",
            password = "wrong-password"
        });

        var json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("message").GetString().Should().Be("Username hoặc mật khẩu không đúng.");
    }

    [Fact]
    public async Task JobController_GetJobs_Should_Return_200_With_Expected_Format()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/jobs?page=1&pageSize=10&keyword=.NET");

        var json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        json.RootElement.GetProperty("data").GetProperty("meta").GetProperty("page").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task JobController_HrJobs_Should_Return_401_Without_Token_And_403_For_Candidate()
    {
        HttpResponseMessage unauthorized = await _client.GetAsync("/api/hr/jobs");
        var unauthorizedJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(unauthorized);
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unauthorizedJson.RootElement.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();

        string candidateToken = _factory.Fixture.CreateJwt(TestDataSeeder.CandidateUserId.ToString(), "Candidate");
        PostgresTestFixture.SetBearerToken(_client, candidateToken);
        HttpResponseMessage forbidden = await _client.GetAsync("/api/hr/jobs");
        var forbiddenJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(forbidden);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        forbiddenJson.RootElement.GetProperty("message").GetString().Should().Be("Bạn không có quyền");
    }

    [Fact]
    public async Task ApplicationController_ApplyContext_Should_Require_Candidate_And_HrApplications_Should_Forbid_Candidate()
    {
        string candidateToken = _factory.Fixture.CreateJwt(TestDataSeeder.CandidateUserId.ToString(), "Candidate");
        PostgresTestFixture.SetBearerToken(_client, candidateToken);

        HttpResponseMessage applyContext = await _client.GetAsync($"/api/jobs/{TestDataSeeder.ApprovedJobId}/apply-context");
        var applyJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(applyContext);
        applyContext.StatusCode.Should().Be(HttpStatusCode.OK);
        applyJson.RootElement.GetProperty("data").GetProperty("eligibility").GetProperty("canApply").GetBoolean().Should().BeFalse();

        HttpResponseMessage forbidden = await _client.GetAsync("/api/hr/applications?page=1&pageSize=10");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CandidateController_Profile_And_HrCandidates_Should_Return_Controlled_StatusCodes()
    {
        HttpResponseMessage unauthorized = await _client.GetAsync("/api/candidate/profile");
        var unauthorizedJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(unauthorized);
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unauthorizedJson.RootElement.GetProperty("message").GetString().Should().Be("Không có quyền truy cập");

        string candidateToken = _factory.Fixture.CreateJwt(TestDataSeeder.CandidateUserId.ToString(), "Candidate");
        PostgresTestFixture.SetBearerToken(_client, candidateToken);
        HttpResponseMessage profileResponse = await _client.GetAsync("/api/candidate/profile");
        var profileJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(profileResponse);
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        profileJson.RootElement.GetProperty("data").GetProperty("profile").GetProperty("name").GetString().Should().Be("Candidate User");

        string hrToken = _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR");
        PostgresTestFixture.SetBearerToken(_client, hrToken);
        HttpResponseMessage hrCandidates = await _client.GetAsync("/api/hr/candidates?page=1&pageSize=10");
        var candidatesJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(hrCandidates);
        hrCandidates.StatusCode.Should().Be(HttpStatusCode.OK);
        candidatesJson.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task NotificationStream_Should_Return_401_Without_Token()
    {
        // T-SSE-001: the SSE notification stream is authenticated. Without a bearer token the
        // [Authorize] filter rejects the request (401) before any streaming begins, so the response
        // completes immediately rather than hanging on an open event-stream.
        HttpResponseMessage response = await _client.GetAsync(
            "/api/notifications/stream",
            HttpCompletionOption.ResponseHeadersRead);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Public_Apis_Should_Return_Controlled_404_Not_500_For_Invalid_Ids()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/jobs/not-a-guid");

        var json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("message").GetString()!.ToLowerInvariant().Should().Contain("not found");
    }
}
