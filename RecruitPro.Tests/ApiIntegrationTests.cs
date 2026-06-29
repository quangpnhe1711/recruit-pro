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

    // ----------------------------------------------------------------------------------------------
    // Phase 2 security regression tests
    // ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task RecentApplications_PII_Endpoint_Anonymous401_Candidate403_HrOwner200()
    {
        string recentUrl = $"/api/jobs/{TestDataSeeder.ApprovedJobId}/applications/recent";

        HttpResponseMessage anonymous = await _client.GetAsync(recentUrl);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.CandidateUserId.ToString(), "Candidate"));
        HttpResponseMessage candidate = await _client.GetAsync(recentUrl);
        candidate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));
        HttpResponseMessage hrOwner = await _client.GetAsync(recentUrl);
        hrOwner.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task JobApplications_Should_Forbid_Hr_Outside_Job_Ownership_And_Allow_Owner_NotSystemAdmin()
    {
        string url = $"/api/jobs/{TestDataSeeder.ApprovedJobId}/applications?page=1&pageSize=10";

        // HR that owns neither the job nor the department -> 403 even with a valid HR role.
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(Guid.NewGuid().ToString(), "HR"));
        HttpResponseMessage outsider = await _client.GetAsync(url);
        outsider.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));
        HttpResponseMessage owner = await _client.GetAsync(url);
        owner.StatusCode.Should().Be(HttpStatusCode.OK);

        // SystemAdmin-only → 403: Phase 2.2b removed CanAccessJob bypass; no HR/Manager role → forbidden.
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.SystemAdminUserId.ToString(), "SystemAdmin"));
        HttpResponseMessage admin = await _client.GetAsync(url);
        admin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ApplicationReviewDetail_Should_Be_Scoped_To_Owner_And_DeptHead_NotSystemAdmin()
    {
        string url = $"/api/hr/applications/{TestDataSeeder.ApplicationId}";

        // Owning recruiter (HR) -> allowed.
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Department head (Manager role, heads Engineering) -> allowed.
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.ManagerUserId.ToString(), "Manager"));
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);

        // SystemAdmin-only → 403: Phase 2.2b removed the ownership bypass; SystemAdmin is not a
        // business-data superuser and does not satisfy CanAccessApplication without an HR/Manager role.
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.SystemAdminUserId.ToString(), "SystemAdmin"));
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // HR with no ownership over this application -> forbidden, despite the HR role.
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(Guid.NewGuid().ToString(), "HR"));
        HttpResponseMessage outsider = await _client.GetAsync(url);
        outsider.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HrApplicationsList_Should_Be_Empty_For_OutOfScope_Hr_And_NonEmpty_For_Owner_And_Admin()
    {
        const string url = "/api/hr/applications?page=1&pageSize=10";

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(Guid.NewGuid().ToString(), "HR"));
        var outsiderJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(await _client.GetAsync(url));
        outsiderJson.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().Be(0);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));
        var ownerJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(await _client.GetAsync(url));
        ownerJson.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.SystemAdminUserId.ToString(), "SystemAdmin"));
        var adminJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(await _client.GetAsync(url));
        adminJson.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HrCandidateDetail_Should_Hide_Candidate_From_OutOfScope_Hr()
    {
        string url = $"/api/hr/candidates/{TestDataSeeder.CandidateProfileId}";

        // Owning recruiter sees the candidate (reached through an owned application).
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);

        // HR with no owned application for this candidate -> 404 (existence is not leaked).
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(Guid.NewGuid().ToString(), "HR"));
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InterviewEndpoints_Should_No_Longer_Be_Anonymous()
    {
        // Previously these were completely unauthenticated and leaked candidate interview PII.
        HttpResponseMessage hrAnonymous = await _client.GetAsync("/api/hr/interviews?page=1&pageSize=10");
        hrAnonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.CandidateUserId.ToString(), "Candidate"));
        HttpResponseMessage hrAsCandidate = await _client.GetAsync("/api/hr/interviews?page=1&pageSize=10");
        hrAsCandidate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));
        HttpResponseMessage hrAsHr = await _client.GetAsync("/api/hr/interviews?page=1&pageSize=10");
        hrAsHr.StatusCode.Should().Be(HttpStatusCode.OK);

        // The candidate route is scoped to the authenticated candidate; without a token it is rejected.
        _client.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage candidateAnonymous = await _client.GetAsync("/api/candidate/interviews");
        candidateAnonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OfferEndpoints_Should_No_Longer_Be_Anonymous()
    {
        // Sending an offer was previously callable without authentication.
        HttpResponseMessage anonymousGet = await _client.GetAsync($"/api/hr/applications/{TestDataSeeder.ApplicationId}/offer");
        anonymousGet.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.CandidateUserId.ToString(), "Candidate"));
        HttpResponseMessage candidateSend = await _client.PostAsJsonAsync(
            $"/api/hr/applications/{TestDataSeeder.ApplicationId}/offer/send",
            new { });
        candidateSend.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DashboardAndDiscovery_PII_Endpoints_Should_Require_Auth()
    {
        (await _client.GetAsync("/api/hr/dashboard")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync("/api/manager/dashboard")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync("/api/hr/talent-pool/search")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync("/api/copilot/jobs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
