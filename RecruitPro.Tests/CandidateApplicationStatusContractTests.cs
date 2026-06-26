using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;
using RecruitPro.Tests.Infrastructure;

namespace RecruitPro.Tests;

/// <summary>
/// Status-contract regression — the candidate applications API must return the **canonical English**
/// ApplicationStatus enum value in `status`, with localized Vietnamese text only in `statusLabel`. The
/// previous bug returned localized text (e.g. "HR đang sàng lọc") as `status`, which the FE normalized
/// to `Rejected`. See ERROR-CONTRACT / API-CONTRACT (canonical-status rule) and TEST-MATRIX (T-STATUS-001..003).
/// </summary>
public sealed class CandidateApplicationStatusContractTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly RecruitProWebApplicationFactory _factory;
    private HttpClient _client = default!;

    public CandidateApplicationStatusContractTests(PostgresTestFixture fixture)
    {
        _factory = new RecruitProWebApplicationFactory(fixture);
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
        PostgresTestFixture.SetBearerToken(_client, _factory.Fixture.CreateJwt(TestDataSeeder.CandidateUserId.ToString(), "Candidate"));
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync();
    }

    private async Task SetSeededApplicationStatusAsync(ApplicationStatus status)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var application = await db.Applications.SingleAsync(a => a.Id == TestDataSeeder.ApplicationId);
        application.Status = status;
        await db.SaveChangesAsync();
    }

    private async Task<JsonElement> GetFirstItemAsync()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/candidate/applications");
        JsonDocument json = await ApiResponseAssertions.AssertNo500AndEnvelopeAsync(response);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement items = json.RootElement.GetProperty("data").GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);
        // Clone so the element stays valid after the JsonDocument is disposed by the caller scope.
        return items[0].Clone();
    }

    // T-STATUS-001: Screening → canonical "Screening" + localized statusLabel + withdraw available.
    [Fact]
    public async Task CandidateApplications_Screening_ReturnsCanonicalStatusAndStatusLabel()
    {
        await SetSeededApplicationStatusAsync(ApplicationStatus.Screening);

        JsonElement item = await GetFirstItemAsync();

        item.GetProperty("status").GetString().Should().Be("Screening");
        item.GetProperty("statusLabel").GetString().Should().Be("HR đang sàng lọc");
        item.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString()).Should().Contain("withdraw");
    }

    // T-STATUS-002: Interview → canonical "Interview" + localized statusLabel.
    [Fact]
    public async Task CandidateApplications_Interview_ReturnsCanonicalStatusAndStatusLabel()
    {
        await SetSeededApplicationStatusAsync(ApplicationStatus.Interview);

        JsonElement item = await GetFirstItemAsync();

        item.GetProperty("status").GetString().Should().Be("Interview");
        item.GetProperty("statusLabel").GetString().Should().Be("Phỏng vấn");
        item.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString()).Should().Contain("withdraw");
    }

    // T-STATUS-003: Rejected → canonical "Rejected", localized statusLabel, and NO withdraw action.
    [Fact]
    public async Task CandidateApplications_Rejected_ReturnsCanonicalStatusAndNoWithdraw()
    {
        await SetSeededApplicationStatusAsync(ApplicationStatus.Rejected);

        JsonElement item = await GetFirstItemAsync();

        item.GetProperty("status").GetString().Should().Be("Rejected");
        item.GetProperty("statusLabel").GetString().Should().Be("Không phù hợp");
        item.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString()).Should().NotContain("withdraw");
    }
}
