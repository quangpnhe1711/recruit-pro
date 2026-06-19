using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Enums;
using RecruitPro.Tests.Infrastructure;

namespace RecruitPro.Tests;

public sealed class RepositoryIntegrationExpandedTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly RecruitProWebApplicationFactory _factory;

    public RepositoryIntegrationExpandedTests(PostgresTestFixture fixture)
    {
        _factory = new RecruitProWebApplicationFactory(fixture);
    }

    public Task InitializeAsync() => _factory.InitializeAsync();
    public Task DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task JobRepository_GetPendingApprovalPagedAsync_ReturnsSeededPendingJob()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IJobRepository repository = scope.ServiceProvider.GetRequiredService<IJobRepository>();

        var result = await repository.GetPendingApprovalPagedAsync(null, null, 1, 10);

        result.Total.Should().BeGreaterThanOrEqualTo(1);
        result.Jobs.Should().Contain(job => job.Id == TestDataSeeder.PendingJobId && job.Status == JobStatus.PendingApproval);
    }

    [Fact]
    public async Task CandidateProfileRepository_GetPagedAsync_FiltersByKeyword()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ICandidateProfileRepository repository = scope.ServiceProvider.GetRequiredService<ICandidateProfileRepository>();

        var result = await repository.GetPagedAsync(1, 10, "Candidate");

        result.Total.Should().BeGreaterThanOrEqualTo(1);
        result.Candidates.Should().ContainSingle(candidate => candidate.Id == TestDataSeeder.CandidateProfileId);
    }

    [Fact]
    public async Task ApplicationRepository_StatusCounts_AndManagerQueue_ReturnSeededApplication()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IApplicationRepository repository = scope.ServiceProvider.GetRequiredService<IApplicationRepository>();

        Dictionary<ApplicationStatus, int> counts = await repository.GetStatusCountsAsync();
        IReadOnlyList<Domain.Entities.Application> queue = await repository.GetManagerReviewQueueAsync(null);

        counts.GetValueOrDefault(ApplicationStatus.ManagerReview).Should().BeGreaterThanOrEqualTo(1);
        queue.Should().Contain(application => application.Id == TestDataSeeder.ApplicationId);
    }
}
