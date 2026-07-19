using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Domain.Entities;
using RecruitPro.Infrastructure.Data;
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
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_CREDENTIALS");
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
        forbiddenJson.RootElement.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");
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
        unauthorizedJson.RootElement.GetProperty("errorCode").GetString().Should().Be("UNAUTHENTICATED");

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
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("JOB_NOT_FOUND");
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
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.SecondHrUserId.ToString(), "HR"));
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
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.SecondHrUserId.ToString(), "HR"));
        HttpResponseMessage outsider = await _client.GetAsync(url);
        outsider.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HrApplicationsList_Should_Be_Empty_For_OutOfScope_Hr_NonEmpty_For_Owner_And_Forbid_SystemAdminOnly()
    {
        const string url = "/api/hr/applications?page=1&pageSize=10";

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.SecondHrUserId.ToString(), "HR"));
        var outsiderJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(await _client.GetAsync(url));
        outsiderJson.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().Be(0);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));
        var ownerJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(await _client.GetAsync(url));
        ownerJson.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.SystemAdminUserId.ToString(), "SystemAdmin"));
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HrCandidateDetail_Should_Hide_Candidate_From_OutOfScope_Hr()
    {
        string url = $"/api/hr/candidates/{TestDataSeeder.CandidateProfileId}";

        // Owning recruiter sees the candidate (reached through an owned application).
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);

        // HR with no owned application for this candidate -> 404 (existence is not leaked).
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.SecondHrUserId.ToString(), "HR"));
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
    public async Task CopilotLatestFitAnalysis_Should_Enforce_Role_And_Ownership_And_Return_Newest()
    {
        await SeedFitAnalysesAsync();
        string url = $"/api/copilot/applications/{TestDataSeeder.ApplicationId}/fit-analysis/latest";

        HttpResponseMessage anonymous = await _client.GetAsync(url);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.CandidateUserId.ToString(), "Candidate"));
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.SecondHrUserId.ToString(), "HR"));
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.ManagerUserId.ToString(), "Manager"));
        HttpResponseMessage managerResponse = await _client.GetAsync(url);
        managerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));
        var json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(await _client.GetAsync(url));
        json.RootElement.GetProperty("data").GetProperty("fitLabel").GetString().Should().Be("StrongFit");
        json.RootElement.GetProperty("data").GetProperty("totalScore").GetDecimal().Should().Be(91);
        json.RootElement.GetProperty("data").GetProperty("strengths").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task CopilotLatestFitAnalysis_WhenMissing_Should_Return_Clean_404()
    {
        string url = $"/api/copilot/applications/{TestDataSeeder.ApplicationId}/fit-analysis/latest";
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));

        var json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(await _client.GetAsync(url));

        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("statusCode").GetInt32().Should().Be(404);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("ENTITY_NOT_FOUND");
    }

    [Fact]
    public async Task CopilotArtifacts_Should_Return_Current_User_History_And_Apply_Filters()
    {
        await SeedArtifactsAsync();
        string url = $"/api/copilot/artifacts?jobId={TestDataSeeder.ApprovedJobId}&artifactType=candidate_search&take=20";

        HttpResponseMessage anonymous = await _client.GetAsync(url);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.CandidateUserId.ToString(), "Candidate"));
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));
        var json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(await _client.GetAsync(url));

        json.RootElement.GetProperty("data").GetArrayLength().Should().Be(1);
        JsonElement artifact = json.RootElement.GetProperty("data")[0];
        artifact.GetProperty("artifactType").GetString().Should().Be("candidate_search");
        artifact.GetProperty("ownerUserId").GetGuid().Should().Be(TestDataSeeder.HrUserId);
        artifact.GetProperty("payloadJson").GetString().Should().Contain("Candidate User");

        string emptyUrl = $"/api/copilot/artifacts?jobId={TestDataSeeder.PendingJobId}&artifactType=email_draft";
        var emptyJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(await _client.GetAsync(emptyUrl));
        emptyJson.RootElement.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task CopilotPromptTemplateEndpoints_Should_Enforce_Roles_And_Persist_For_Current_User()
    {
        const string url = "/api/copilot/prompt-templates";

        HttpResponseMessage anonymous = await _client.GetAsync(url);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.CandidateUserId.ToString(), "Candidate"));
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        HttpResponseMessage candidateCreate = await _client.PostAsJsonAsync(url, new
        {
            name = "Nope",
            templateType = "candidate_search",
            prompt = "Should not persist",
            isActive = true
        });
        candidateCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));
        HttpResponseMessage created = await _client.PostAsJsonAsync(url, new
        {
            name = "Search backend",
            templateType = "candidate_search",
            prompt = "Find backend candidates with SQL evidence",
            isActive = true
        });
        var createdJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(created);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        createdJson.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("Search backend");

        var listJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(await _client.GetAsync(url));
        listJson.RootElement.GetProperty("data").GetArrayLength().Should().Be(1);
        listJson.RootElement.GetProperty("data")[0].GetProperty("templateType").GetString().Should().Be("candidate_search");
    }

    [Fact]
    public async Task DashboardAndDiscovery_PII_Endpoints_Should_Require_Auth()
    {
        (await _client.GetAsync("/api/hr/dashboard")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync("/api/manager/dashboard")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync("/api/hr/talent-pool/search")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync("/api/copilot/jobs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CopilotRanking_Screening_Idempotency_And_PassCv_Flow()
    {
        Guid screeningAppId = await SeedScreeningApplicationAsync();
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));

        // Create (or reuse) the conversation for the approved job.
        var convResponse = await _client.PostAsJsonAsync("/api/copilot/conversations", new { jobId = TestDataSeeder.ApprovedJobId });
        var convJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(convResponse);
        Guid conversationId = convJson.RootElement.GetProperty("data").GetProperty("conversationId").GetGuid();

        // Run ranking — only Screening applications are eligible; the seeded ManagerReview application is excluded.
        object rankRequest = new { jobId = TestDataSeeder.ApprovedJobId, prompt = "Xếp hạng ứng viên .NET và SQL", forceRanking = true };
        var rankJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(
            await _client.PostAsJsonAsync($"/api/copilot/conversations/{conversationId}/rankings", rankRequest));
        JsonElement rankData = rankJson.RootElement.GetProperty("data");
        rankData.GetProperty("didRank").GetBoolean().Should().BeTrue();
        Guid rankingSessionId = rankData.GetProperty("rankingSessionId").GetGuid();

        List<Guid> rankedAppIds = rankData.GetProperty("results").EnumerateArray()
            .Select(result => result.GetProperty("applicationId").GetGuid())
            .ToList();
        rankedAppIds.Should().Contain(screeningAppId);
        rankedAppIds.Should().NotContain(TestDataSeeder.ApplicationId); // seeded app is ManagerReview → excluded

        // Idempotency: identical input returns the latest matching session, no duplicate/no provider.
        var rerunJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(
            await _client.PostAsJsonAsync($"/api/copilot/conversations/{conversationId}/rankings", rankRequest));
        JsonElement rerunData = rerunJson.RootElement.GetProperty("data");
        rerunData.GetProperty("reusedRankingSession").GetBoolean().Should().BeTrue();
        rerunData.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).Should().Contain("ranking-session:reused");

        // Pass CV: move the screening candidate to Head Review (ManagerReview).
        var passJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(
            await _client.PostAsJsonAsync($"/api/copilot/ranking-sessions/{rankingSessionId}/pass-cv",
                new { applicationIds = new[] { screeningAppId } }));
        JsonElement passData = passJson.RootElement.GetProperty("data");
        passData.GetProperty("updated").GetArrayLength().Should().Be(1);
        passData.GetProperty("updated")[0].GetProperty("newStatus").GetString().Should().Be("ManagerReview");

        (await _factory.Fixture.GetApplicationStatusRawAsync(screeningAppId)).Should().Be("ManagerReview");
    }

    [Fact]
    public async Task CopilotChat_NonRankingPrompt_RoutesToProvider_WithoutRanking()
    {
        // Scope is now the AI's judgment (system prompt), not a backend keyword gate: a non-ranking
        // chat prompt is forwarded to the provider and returned as a chat reply, with no ranking run
        // and no ranking session. The soft-decline for genuinely off-topic prompts happens inside the
        // AI and isn't exercised here (the fake provider always replies).
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));
        var convJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(
            await _client.PostAsJsonAsync("/api/copilot/conversations", new { jobId = TestDataSeeder.ApprovedJobId }));
        Guid conversationId = convJson.RootElement.GetProperty("data").GetProperty("conversationId").GetGuid();

        var replyJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(
            await _client.PostAsJsonAsync($"/api/copilot/conversations/{conversationId}/rankings",
                new { jobId = TestDataSeeder.ApprovedJobId, prompt = "JD này có yêu cầu Python không?", forceRanking = false }));
        JsonElement data = replyJson.RootElement.GetProperty("data");
        data.GetProperty("didRank").GetBoolean().Should().BeFalse();
        data.GetProperty("assistantMessage").GetString().Should().Be("Test reply");
        data.GetProperty("rankingSessionId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task CopilotDeprecatedEndpoints_ReturnDeprecationWarning_NoProvider()
    {
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));

        var searchJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(
            await _client.PostAsJsonAsync("/api/copilot/candidate-search",
                new { jobId = TestDataSeeder.ApprovedJobId, query = "Tìm ứng viên .NET", maxResults = 5 }));
        searchJson.RootElement.GetProperty("data").GetProperty("ai").GetProperty("warnings")
            .EnumerateArray().Select(w => w.GetString())
            .Should().Contain(w => w!.StartsWith("candidate-search:deprecated", StringComparison.Ordinal));

        var emailJson = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(
            await _client.PostAsJsonAsync($"/api/copilot/applications/{TestDataSeeder.ApplicationId}/emails/draft",
                new { templateType = "interview_invite", tone = "warm" }));
        emailJson.RootElement.GetProperty("data").GetProperty("ai").GetProperty("warnings")
            .EnumerateArray().Select(w => w.GetString())
            .Should().Contain(w => w!.StartsWith("email-draft:deprecated", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CopilotPassCv_EnforcesAuthAndOwnership()
    {
        string url = $"/api/copilot/ranking-sessions/{Guid.NewGuid()}/pass-cv";
        object body = new { applicationIds = new[] { TestDataSeeder.ApplicationId } };

        // Anonymous → 401.
        (await _client.PostAsJsonAsync(url, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Candidate role is blocked at the [Authorize(Roles="HR,Manager")] layer → 403.
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.CandidateUserId.ToString(), "Candidate"));
        (await _client.PostAsJsonAsync(url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // HR but the ranking session does not belong to them (or does not exist) → 404, never a silent move.
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.HrUserId.ToString(), "HR"));
        (await _client.PostAsJsonAsync(url, body)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Seeds a second candidate with a Screening application on the approved job so the ranking pool
    /// has a rank-eligible candidate (the pre-seeded application is ManagerReview and thus excluded).
    /// </summary>
    private async Task<Guid> SeedScreeningApplicationAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DateTime now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Guid userId = Guid.Parse("21000000-0000-0000-0000-000000000009");
        Guid applicationId = Guid.Parse("71000000-0000-0000-0000-000000000009");

        db.Users.Add(new User
        {
            Id = userId,
            Username = "screening.candidate",
            Email = "screening@recruitpro.test",
            FullName = "Screening Candidate",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass@123"),
            Phone = "0900009009",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.UserRoles.Add(new UserRole { UserId = userId, RoleId = TestDataSeeder.CandidateRoleId, AssignedAt = now });
        db.Applications.Add(new Domain.Entities.Application
        {
            Id = applicationId,
            UserId = userId,
            JobId = TestDataSeeder.ApprovedJobId,
            AssignedRecruiterId = TestDataSeeder.HrUserId,
            AssignedDepartmentHeadId = TestDataSeeder.ManagerUserId,
            Status = RecruitPro.Domain.Enums.ApplicationStatus.Screening,
            AppliedAt = now,
            RuleScore = 70,
            FinalScore = 70,
            ScoreStatus = "Completed",
            ScoredAt = now
        });
        await db.SaveChangesAsync();
        return applicationId;
    }

    private async Task SeedFitAnalysesAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DateTime older = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-2), DateTimeKind.Unspecified);
        DateTime newer = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-1), DateTimeKind.Unspecified);

        db.CandidateFitAnalyses.AddRange(
            new CandidateFitAnalysis
            {
                Id = Guid.NewGuid(),
                AuditId = Guid.NewGuid(),
                JobId = TestDataSeeder.ApprovedJobId,
                CandidateUserId = TestDataSeeder.CandidateUserId,
                ApplicationId = TestDataSeeder.ApplicationId,
                FitLabel = "PotentialFit",
                ConfidenceScore = 70,
                TotalScore = 72,
                StrengthsJson = JsonSerializer.Serialize(new[] { ".NET" }),
                GapsJson = JsonSerializer.Serialize(new[] { "SQL depth" }),
                EvidenceJson = JsonSerializer.Serialize(new[] { "older snapshot" }),
                Summary = "Older snapshot",
                ProviderName = "deterministic-copilot",
                ModelName = "deterministic-copilot-v2",
                FallbackUsed = true,
                CreatedAt = older
            },
            new CandidateFitAnalysis
            {
                Id = Guid.NewGuid(),
                AuditId = Guid.NewGuid(),
                JobId = TestDataSeeder.ApprovedJobId,
                CandidateUserId = TestDataSeeder.CandidateUserId,
                ApplicationId = TestDataSeeder.ApplicationId,
                FitLabel = "StrongFit",
                ConfidenceScore = 94,
                TotalScore = 91,
                StrengthsJson = JsonSerializer.Serialize(new[] { ".NET", "SQL" }),
                GapsJson = JsonSerializer.Serialize(Array.Empty<string>()),
                EvidenceJson = JsonSerializer.Serialize(new[] { "newest snapshot" }),
                Summary = "Newest snapshot",
                ProviderName = "deterministic-copilot",
                ModelName = "deterministic-copilot-v2",
                FallbackUsed = true,
                CreatedAt = newer
            });

        await db.SaveChangesAsync();
    }

    private async Task SeedArtifactsAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DateTime now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        db.CopilotGeneratedArtifacts.AddRange(
            new CopilotGeneratedArtifact
            {
                Id = Guid.NewGuid(),
                OwnerUserId = TestDataSeeder.HrUserId,
                JobId = TestDataSeeder.ApprovedJobId,
                ApplicationId = TestDataSeeder.ApplicationId,
                ArtifactType = "candidate_search",
                Prompt = "Find backend candidates",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    query = "Find backend candidates",
                    results = new[] { new { fullName = "Candidate User", evidence = ".NET and SQL" } }
                }),
                ProviderName = "deterministic-copilot",
                ModelName = "deterministic-copilot-v2",
                FallbackUsed = true,
                CreatedAt = now
            },
            new CopilotGeneratedArtifact
            {
                Id = Guid.NewGuid(),
                OwnerUserId = TestDataSeeder.HrUserId,
                JobId = TestDataSeeder.ApprovedJobId,
                ArtifactType = "shortlist_suggestion",
                Prompt = "Shortlist",
                PayloadJson = JsonSerializer.Serialize(new { suggestions = Array.Empty<object>() }),
                ProviderName = "deterministic-copilot",
                ModelName = "deterministic-copilot-v2",
                FallbackUsed = true,
                CreatedAt = now.AddMinutes(-5)
            },
            new CopilotGeneratedArtifact
            {
                Id = Guid.NewGuid(),
                OwnerUserId = TestDataSeeder.ManagerUserId,
                JobId = TestDataSeeder.ApprovedJobId,
                ArtifactType = "candidate_search",
                Prompt = "Manager artifact",
                PayloadJson = "{}",
                ProviderName = "deterministic-copilot",
                ModelName = "deterministic-copilot-v2",
                FallbackUsed = true,
                CreatedAt = now.AddMinutes(-10)
            });

        await db.SaveChangesAsync();
    }
}
