using Microsoft.Extensions.DependencyInjection;
using RecruitPro.Application.DTOs.Request.Departments;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Tests.Infrastructure;

namespace RecruitPro.Tests;

/// <summary>
/// Phase 2/3 — ownership API surface against real PostgreSQL: department head exposure/assignment,
/// the assignable-owners directory, job recruiter persistence, and ownership fields on job DTOs.
/// See docs/source-of-truth/API-CONTRACT.md and TEST-MATRIX.md (T-OWN-010..017).
/// </summary>
public sealed class OwnershipServiceIntegrationTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly RecruitProWebApplicationFactory _factory;

    public OwnershipServiceIntegrationTests(PostgresTestFixture fixture)
    {
        _factory = new RecruitProWebApplicationFactory(fixture);
    }

    public Task InitializeAsync() => _factory.InitializeAsync();
    public Task DisposeAsync() => _factory.DisposeAsync();

    // T-OWN-010: department detail exposes the head user info.
    [Fact]
    public async Task Department_ReturnsHeadUserInfo()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IJobService service = scope.ServiceProvider.GetRequiredService<IJobService>();

        var response = await service.GetDepartmentAsync(TestDataSeeder.DepartmentId.ToString());

        response.Success.Should().BeTrue();
        response.Data!.HeadUserId.Should().Be(TestDataSeeder.ManagerUserId.ToString());
        response.Data.HeadUserName.Should().Be("Manager User");
        response.Data.HeadUserEmail.Should().Be("manager@recruitpro.test");
    }

    // T-OWN-011: updating the department head persists and returns the new head.
    [Fact]
    public async Task Department_Update_SetsHeadUserId()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IJobService service = scope.ServiceProvider.GetRequiredService<IJobService>();

        var response = await service.UpdateDepartmentAsync(
            TestDataSeeder.DepartmentId.ToString(),
            new UpdateDepartmentRequest { HeadUserId = TestDataSeeder.HeadDepartmentUserId.ToString() });

        response.Success.Should().BeTrue();
        response.Data!.HeadUserId.Should().Be(TestDataSeeder.HeadDepartmentUserId.ToString());

        var reloaded = await service.GetDepartmentAsync(TestDataSeeder.DepartmentId.ToString());
        reloaded.Data!.HeadUserId.Should().Be(TestDataSeeder.HeadDepartmentUserId.ToString());
    }

    // T-OWN-011 (negative): a non-HeadDepartment/SystemAdmin user cannot be set as the head.
    [Fact]
    public async Task Department_Update_RejectsCandidateAsHead()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IJobService service = scope.ServiceProvider.GetRequiredService<IJobService>();

        var response = await service.UpdateDepartmentAsync(
            TestDataSeeder.DepartmentId.ToString(),
            new UpdateDepartmentRequest { HeadUserId = TestDataSeeder.CandidateUserId.ToString() });

        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be("INVALID_DEPARTMENT_HEAD");
    }

    // T-OWN-012: candidates never appear in the assignable recruitment owners lists.
    [Fact]
    public async Task AssignableRecruitmentOwners_ExcludesCandidates()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserService service = scope.ServiceProvider.GetRequiredService<IUserService>();

        var response = await service.GetAssignableRecruitmentOwnersAsync();

        response.Success.Should().BeTrue();
        string candidateId = TestDataSeeder.CandidateUserId.ToString();
        response.Data!.Recruiters.Should().NotContain(owner => owner.Id == candidateId);
        response.Data.DepartmentHeads.Should().NotContain(owner => owner.Id == candidateId);
    }

    // T-OWN-013: recruiters = HR users; departmentHeads = HeadDepartment users.
    [Fact]
    public async Task AssignableRecruitmentOwners_ReturnsHrAndHeadDepartment()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserService service = scope.ServiceProvider.GetRequiredService<IUserService>();

        var response = await service.GetAssignableRecruitmentOwnersAsync();

        response.Data!.Recruiters.Should().Contain(owner => owner.Id == TestDataSeeder.HrUserId.ToString());
        response.Data.DepartmentHeads.Should().Contain(owner => owner.Id == TestDataSeeder.HeadDepartmentUserId.ToString());
    }

    // T-OWN-014: creating a job persists the supplied recruiter (an HR user).
    [Fact]
    public async Task CreateJob_PersistsRecruiterId()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IJobService service = scope.ServiceProvider.GetRequiredService<IJobService>();

        var create = await service.CreateJobAsync(new CreateJobRequest
        {
            Title = "Backend Engineer II",
            DepartmentId = TestDataSeeder.DepartmentId.ToString(),
            RecruiterId = TestDataSeeder.HrUserId.ToString(),
            Location = "HCMC",
            Description = "Build services",
            VacancyCount = 1,
            SkillIds = [TestDataSeeder.DotNetSkillId.ToString()]
        }, TestDataSeeder.ManagerUserId);

        create.StatusCode.Should().Be(201);
        var detail = await service.GetJobDetailAsync(create.Data!.JobId);
        detail.Data!.RecruiterId.Should().Be(TestDataSeeder.HrUserId.ToString());
    }

    // T-OWN-015: omitting the recruiter falls back to the creating user (compatibility fallback).
    [Fact]
    public async Task CreateJob_DefaultsRecruiterToCurrentHr_WhenMissing()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IJobService service = scope.ServiceProvider.GetRequiredService<IJobService>();

        var create = await service.CreateJobAsync(new CreateJobRequest
        {
            Title = "Backend Engineer III",
            DepartmentId = TestDataSeeder.DepartmentId.ToString(),
            Location = "HCMC",
            Description = "Build services",
            VacancyCount = 1,
            SkillIds = [TestDataSeeder.DotNetSkillId.ToString()]
        }, TestDataSeeder.HrUserId);

        create.StatusCode.Should().Be(201);
        var detail = await service.GetJobDetailAsync(create.Data!.JobId);
        detail.Data!.RecruiterId.Should().Be(TestDataSeeder.HrUserId.ToString());
    }

    // T-OWN-016: job detail returns recruiter and department head.
    [Fact]
    public async Task JobDetail_ReturnsRecruiterAndDepartmentHead()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IJobService service = scope.ServiceProvider.GetRequiredService<IJobService>();

        var response = await service.GetJobDetailAsync(TestDataSeeder.ApprovedJobId.ToString());

        response.Success.Should().BeTrue();
        response.Data!.RecruiterId.Should().Be(TestDataSeeder.HrUserId.ToString());
        response.Data.RecruiterName.Should().Be("HR User");
        response.Data.DepartmentHeadId.Should().Be(TestDataSeeder.ManagerUserId.ToString());
        response.Data.DepartmentHeadName.Should().Be("Manager User");
    }

    // T-OWN-017: the HR job list returns the effective department head.
    [Fact]
    public async Task JobList_ReturnsEffectiveDepartmentHead()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IJobService service = scope.ServiceProvider.GetRequiredService<IJobService>();

        var response = await service.GetHrJobsAsync(new HrJobQueryRequest { Page = 1, PageSize = 50 }, TestDataSeeder.HrUserId);

        response.Success.Should().BeTrue();
        response.Data!.Items.Should().Contain(item =>
            item.Id == TestDataSeeder.ApprovedJobId.ToString()
            && item.EffectiveDepartmentHeadId == TestDataSeeder.ManagerUserId.ToString());
    }
}
