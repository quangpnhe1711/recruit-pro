using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.DTOs.Request.Candidate;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.Exceptions;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Domain.Enums;
using RecruitPro.Infrastructure.Data;
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

        var response = await service.CandidateLoginAsync("candidate.user", "Pass@123");

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

        Func<Task> act = () => service.InternalLoginAsync("candidate.user", "Pass@123");

        await act.Should().ThrowAsync<UnauthorizeException>()
            .WithMessage("Tài khoản không có quyền truy cập cổng này.");
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

    // TEST-E2E-APPLICATION-001: end-to-end proof against real PostgreSQL that withdraw → re-apply no
    // longer conflicts and that apply never surfaces HTTP 500 (covers BUG-APPLICATION-001/002).
    [Fact]
    public async Task ApplicationService_WithdrawThenReapply_SucceedsWithoutConflictOr500()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IApplicationService service = scope.ServiceProvider.GetRequiredService<IApplicationService>();

        // The seeded candidate has an active (ManagerReview) application on the approved job.
        var withdraw = await service.WithdrawApplicationAsync(
            TestDataSeeder.CandidateUserId,
            TestDataSeeder.ApplicationId.ToString());

        withdraw.Success.Should().BeTrue();
        withdraw.StatusCode.Should().Be(200);

        // Re-apply must now be allowed (the withdrawn application is closed) and must not 500.
        var reapply = await service.ApplyAsync(
            TestDataSeeder.CandidateUserId,
            TestDataSeeder.ApprovedJobId.ToString(),
            new ApplyJobRequest { CoverLetter = "Re-applying after withdrawal." });

        reapply.Success.Should().BeTrue();
        reapply.StatusCode.Should().Be(201);

        // A second apply against the now-active application is a clean 409 conflict, never 500.
        var duplicate = await service.ApplyAsync(
            TestDataSeeder.CandidateUserId,
            TestDataSeeder.ApprovedJobId.ToString(),
            new ApplyJobRequest());

        duplicate.StatusCode.Should().Be(409);
        duplicate.Message.Should().Be("Candidate already applied for this job.");
    }

    // T-DUP-004 / INV-014: a TRULY concurrent double-apply for the same candidate/job must yield
    // exactly one success and one conflict, and the database must end with exactly one active
    // application. The service-level EXISTS check alone cannot guarantee this under a race (both
    // requests can read "no active application" before either commits); the partial unique index
    // ux_applications_active_user_job is what makes the loser fail and map to 409.
    [Fact]
    public async Task ConcurrentApply_AllowsOnlyOneActiveApplication()
    {
        // Free the job up: the seeded candidate starts with an active application on the approved job.
        using (IServiceScope setupScope = _factory.Services.CreateScope())
        {
            IApplicationService setupService = setupScope.ServiceProvider.GetRequiredService<IApplicationService>();
            var withdraw = await setupService.WithdrawApplicationAsync(
                TestDataSeeder.CandidateUserId,
                TestDataSeeder.ApplicationId.ToString());
            withdraw.StatusCode.Should().Be(200);
        }

        string jobId = TestDataSeeder.ApprovedJobId.ToString();

        // Two independent scopes => two independent DbContexts/connections, fired in parallel.
        using IServiceScope scopeA = _factory.Services.CreateScope();
        using IServiceScope scopeB = _factory.Services.CreateScope();
        IApplicationService serviceA = scopeA.ServiceProvider.GetRequiredService<IApplicationService>();
        IApplicationService serviceB = scopeB.ServiceProvider.GetRequiredService<IApplicationService>();

        var applyA = Task.Run(() => serviceA.ApplyAsync(TestDataSeeder.CandidateUserId, jobId, new ApplyJobRequest { CoverLetter = "A" }));
        var applyB = Task.Run(() => serviceB.ApplyAsync(TestDataSeeder.CandidateUserId, jobId, new ApplyJobRequest { CoverLetter = "B" }));

        var results = await Task.WhenAll(applyA, applyB);

        int created = results.Count(result => result.StatusCode == 201);
        int conflict = results.Count(result => result.StatusCode == 409);
        created.Should().Be(1, "exactly one concurrent apply may create an active application");
        conflict.Should().Be(1, "the losing concurrent apply must be a clean 409, never 500");
        results.Should().OnlyContain(result => result.StatusCode == 201 || result.StatusCode == 409);

        // The database must hold exactly one ACTIVE application for this candidate/job.
        using IServiceScope verifyScope = _factory.Services.CreateScope();
        AppDbContext context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        int activeCount = await context.Applications.CountAsync(application =>
            application.UserId == TestDataSeeder.CandidateUserId
            && application.JobId == TestDataSeeder.ApprovedJobId
            && application.Status != ApplicationStatus.Hired
            && application.Status != ApplicationStatus.Rejected
            && application.Status != ApplicationStatus.OfferDeclined
            && application.Status != ApplicationStatus.Withdrawn);
        activeCount.Should().Be(1);
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
