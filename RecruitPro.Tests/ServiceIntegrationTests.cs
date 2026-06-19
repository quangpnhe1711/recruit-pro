using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.DTOs.Request.Candidate;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Tests.Infrastructure;

namespace RecruitPro.Tests;

public sealed class ServiceIntegrationTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly RecruitProWebApplicationFactory _factory;

    public ServiceIntegrationTests(PostgresTestFixture fixture)
    {
        _factory = new RecruitProWebApplicationFactory(fixture);
    }

    public Task InitializeAsync() => _factory.InitializeAsync();
    public Task DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task AuthService_CandidateLoginAsync_ReturnsTokens_AndCandidateUser()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IAuthService service = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var response = await service.CandidateLoginAsync("candidate@recruitpro.test", "Pass@123");

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.User.Email.Should().Be("candidate@recruitpro.test");
        response.Data.AccessToken.Should().NotBeNullOrWhiteSpace();
        response.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AuthService_InternalLoginAsync_RejectsCandidatePortalMismatch()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IAuthService service = scope.ServiceProvider.GetRequiredService<IAuthService>();

        Func<Task> act = () => service.InternalLoginAsync("candidate@recruitpro.test", "Pass@123");

        await act.Should().ThrowAsync<UnauthorizeException>()
            .WithMessage("User does not have permission to access this portal.");
    }

    [Fact]
    public async Task JobService_SearchJobsAsync_ReturnsApprovedSeededJob()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IJobService service = scope.ServiceProvider.GetRequiredService<IJobService>();

        var response = await service.SearchJobsAsync(new JobQueryRequest
        {
            Keyword = ".NET",
            Page = 1,
            PageSize = 10
        });

        response.Success.Should().BeTrue();
        response.Data!.Items.Should().ContainSingle();
        response.Data.Items[0].Title.Should().Be("Senior .NET Engineer");
    }

    [Fact]
    public async Task JobService_CreatePatchDeleteJob_WorksAcrossLifecycle()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IJobService service = scope.ServiceProvider.GetRequiredService<IJobService>();

        var createResponse = await service.CreateJobAsync(new CreateJobRequest
        {
            Title = "QA Automation Engineer",
            DepartmentId = TestDataSeeder.DepartmentId.ToString(),
            Location = "HCMC",
            Description = "Own quality automation",
            Requirements = ["Playwright", "API testing"],
            Benefits = ["Laptop"],
            VacancyCount = 1,
            SalaryMin = 1000,
            SalaryMax = 1500,
            SkillIds = [TestDataSeeder.DotNetSkillId.ToString()]
        }, TestDataSeeder.HrUserId);

        createResponse.StatusCode.Should().Be(201);
        createResponse.Data!.ApprovalStatus.Should().Be("PendingApproval");

        string createdJobId = createResponse.Data.JobId;
        var patchResponse = await service.PatchJobAsync(createdJobId, new PatchJobRequest
        {
            Title = "QA Automation Engineer II",
            ApprovalStatus = "Approved"
        });

        patchResponse.Success.Should().BeTrue();
        patchResponse.Data!.ApprovalStatus.Should().Be("Approved");

        var deleteResponse = await service.DeleteJobAsync(createdJobId);

        deleteResponse.Success.Should().BeTrue();
        deleteResponse.Message.Should().Be("Job deleted successfully");
    }

    [Fact]
    public async Task CandidateService_GetProfileAsync_ReturnsSeededProfile()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ICandidateService service = scope.ServiceProvider.GetRequiredService<ICandidateService>();

        var response = await service.GetProfileAsync(TestDataSeeder.CandidateUserId);

        response.Success.Should().BeTrue();
        response.Data!.Profile.Name.Should().Be("Candidate User");
        response.Data.Skills.Should().HaveCount(2);
        response.Data.Resume.Should().NotBeNull();
    }

    [Fact]
    public async Task CandidateService_UpdateSkillsAsync_PersistsReplacementSkills()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ICandidateService service = scope.ServiceProvider.GetRequiredService<ICandidateService>();

        var response = await service.UpdateSkillsAsync(TestDataSeeder.CandidateUserId, new UpdateCandidateSkillsRequest
        {
            Skills =
            [
                new CandidateSkillUpsertRequest
                {
                    SkillId = TestDataSeeder.SqlSkillId.ToString(),
                    YearsOfExperience = 6
                }
            ]
        });

        response.Success.Should().BeTrue();
        response.Data!.Skills.Should().Contain(skill =>
            skill.Id == TestDataSeeder.SqlSkillId.ToString()
            && skill.Label == "SQL"
            && skill.YearsOfExperience == 6);
    }

    [Fact]
    public async Task DashboardService_GetHrDashboardAsync_ReturnsSeededCounts()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IDashboardService service = scope.ServiceProvider.GetRequiredService<IDashboardService>();

        var response = await service.GetHrDashboardAsync();

        response.Success.Should().BeTrue();
        response.Data!.Stats.ActivePostings.Should().BeGreaterThanOrEqualTo(1);
        response.Data.Stats.TotalApplicants.Should().BeGreaterThanOrEqualTo(1);
        response.Data.RecentApplications.Should().NotBeEmpty();
        response.Data.PendingApprovals.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DashboardService_GetManagerDashboardAsync_ReturnsApprovalAndFunnelData()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IDashboardService service = scope.ServiceProvider.GetRequiredService<IDashboardService>();

        var response = await service.GetManagerDashboardAsync();

        response.Success.Should().BeTrue();
        response.Data!.Summary.PendingApprovals.Should().BeGreaterThanOrEqualTo(1);
        response.Data.RecruitmentFunnel.Should().NotBeEmpty();
        response.Data.FinalDecisions.Should().NotBeEmpty();
    }
}
