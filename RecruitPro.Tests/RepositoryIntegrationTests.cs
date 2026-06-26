using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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

    // T-DUP-004 / INV-014: the partial unique index (ux_applications_active_user_job) is the DB-level
    // guarantee of "at most one active application per (candidate, job)". The seeded application is
    // ManagerReview (active) for CandidateUserId + ApprovedJobId; a second ACTIVE row must be rejected
    // by the database even if the service-level check were bypassed (e.g. a concurrent double-apply).
    [Fact]
    public async Task ActiveApplicationUniqueIndex_RejectsSecondActiveApplication_ForSameUserAndJob()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.Applications.Add(new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = TestDataSeeder.CandidateUserId,
            JobId = TestDataSeeder.ApprovedJobId,
            Status = ApplicationStatus.Applied,
            AppliedAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Unspecified)
        });

        Func<Task> act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // INV-014 complement: a CLOSED duplicate (Withdrawn) is excluded from the index filter, so it is
    // allowed — this is what keeps re-apply (INV-007) possible.
    [Fact]
    public async Task ActiveApplicationUniqueIndex_AllowsClosedDuplicate_ForSameUserAndJob()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.Applications.Add(new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = TestDataSeeder.CandidateUserId,
            JobId = TestDataSeeder.ApprovedJobId,
            Status = ApplicationStatus.Withdrawn,
            AppliedAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Unspecified)
        });

        Func<Task> act = () => context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
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
