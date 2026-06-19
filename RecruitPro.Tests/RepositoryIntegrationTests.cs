using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;
using RecruitPro.Tests.Infrastructure;

namespace RecruitPro.Tests;

public sealed class RepositoryIntegrationTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly RecruitProWebApplicationFactory _factory;

    public RepositoryIntegrationTests(PostgresTestFixture fixture)
    {
        _factory = new RecruitProWebApplicationFactory(fixture);
    }

    public Task InitializeAsync() => _factory.InitializeAsync();
    public Task DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public void ServiceProvider_Resolves_TargetRepositories_And_DbContext()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<AppDbContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IJobRepository>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IApplicationRepository>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ICandidateProfileRepository>().Should().NotBeNull();
    }

    [Fact]
    public async Task JobRepository_SearchApprovedAsync_Should_Translate_Query_And_Load_Navigations()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IJobRepository repository = scope.ServiceProvider.GetRequiredService<IJobRepository>();

        var result = await repository.SearchApprovedAsync(".net", ["full-time"], [".NET"], "salarydesc", 1, 10);

        result.Total.Should().BeGreaterThan(0);
        Job job = result.Jobs.Single();
        job.Department.Should().NotBeNull();
        job.JobSkills.Should().NotBeEmpty();
        job.JobSkills.Select(skill => skill.Skill).Should().OnlyContain(skill => skill != null);
        job.CreatedByNavigation.Should().NotBeNull();
    }

    [Fact]
    public async Task ApplicationRepository_GetByIdAsync_Should_Load_FullGraph_And_Preserve_Enum_String_Mapping()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IApplicationRepository repository = scope.ServiceProvider.GetRequiredService<IApplicationRepository>();

        Domain.Entities.Application? application = await repository.GetByIdAsync(TestDataSeeder.ApplicationId);

        application.Should().NotBeNull();
        application!.Status.Should().Be(ApplicationStatus.ManagerReview);
        application.User.CandidateProfile.Should().NotBeNull();
        application.Job.Department.Should().NotBeNull();
        application.Job.JobSkills.Should().NotBeEmpty();
        application.ReviewedByNavigation.Should().NotBeNull();
        application.ReviewedByNavigation!.UserRoles.Should().NotBeEmpty();

        string? rawStatus = await _factory.Fixture.GetApplicationStatusRawAsync(TestDataSeeder.ApplicationId);
        rawStatus.Should().Be("ManagerReview");
    }

    [Fact]
    public async Task CandidateProfileRepository_ReplaceSkillsAsync_Should_Not_Throw_TrackingConflict_And_Should_Update_Mappings()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ICandidateProfileRepository repository = scope.ServiceProvider.GetRequiredService<ICandidateProfileRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<RecruitPro.Application.Interfaces.IUnitOfWork>();

        CandidateProfile profile = (await repository.GetTrackedByIdAsync(TestDataSeeder.CandidateProfileId))!;
        profile.Should().NotBeNull();

        List<CandidateSkill> replacement =
        [
            new() { CandidateId = profile.Id, SkillId = TestDataSeeder.DotNetSkillId, YearsOfExperience = 5 },
            new() { CandidateId = profile.Id, SkillId = TestDataSeeder.SqlSkillId, YearsOfExperience = 2 }
        ];

        Func<Task> act = async () =>
        {
            await repository.ReplaceSkillsAsync(profile.Id, replacement);
            await repository.UpdateAsync(profile);
            await unitOfWork.SaveChangesAsync();
        };

        await act.Should().NotThrowAsync();

        CandidateProfile reloaded = (await repository.GetByIdAsync(profile.Id))!;
        reloaded.CandidateSkills.Should().HaveCount(2);
        reloaded.CandidateSkills.Should().Contain(detail => detail.SkillId == TestDataSeeder.DotNetSkillId && detail.YearsOfExperience == 5);
    }
}
