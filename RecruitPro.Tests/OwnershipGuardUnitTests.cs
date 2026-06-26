using AutoMapper;
using Microsoft.Extensions.Logging;
using RecruitPro.Application.DTOs.Request.Applications;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Services;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Tests;

/// <summary>
/// Phase 2/3 — authorization guards (unit, mocked):
///   * Job approve/reject scoped to the department head or SystemAdmin (BR-OWN-003).
///   * ManagerReview (DepartmentHeadReview) advance scoped to the assigned head / SystemAdmin, with a
///     Manager-role fallback only when no head was snapshotted (BR-OWN-007).
/// See TEST-MATRIX.md (T-OWN-018..027).
/// </summary>
public sealed class OwnershipGuardUnitTests
{
    // ---- Job approval guard (T-OWN-018..022) ----

    // T-OWN-018: the department head can approve a job in their department.
    [Fact]
    public async Task DepartmentHead_CanApproveOwnDepartmentJob()
    {
        Guid headId = Guid.NewGuid();
        (JobService service, Job job) = CreateJobServiceWithJob(headId);

        var response = await service.PatchJobAsync(
            job.Id.ToString(),
            new PatchJobRequest { ApprovalStatus = "Approved" },
            headId,
            new[] { "Manager" });

        response.StatusCode.Should().Be(200);
        response.Data!.ApprovalStatus.Should().Be("Approved");
    }

    // T-OWN-019: a non-head, non-admin user cannot approve.
    [Fact]
    public async Task NonDepartmentHead_CannotApproveOtherDepartmentJob()
    {
        Guid headId = Guid.NewGuid();
        (JobService service, Job job) = CreateJobServiceWithJob(headId);

        var response = await service.PatchJobAsync(
            job.Id.ToString(),
            new PatchJobRequest { ApprovalStatus = "Approved" },
            Guid.NewGuid(),
            new[] { "HR", "Manager" });

        response.StatusCode.Should().Be(403);
        response.ErrorCode.Should().Be("FORBIDDEN");
        job.Status.Should().Be(JobStatus.PendingApproval);
    }

    // T-OWN-020: a SystemAdmin can approve any department's job.
    [Fact]
    public async Task SystemAdmin_CanApproveAnyDepartmentJob()
    {
        Guid headId = Guid.NewGuid();
        (JobService service, Job job) = CreateJobServiceWithJob(headId);

        var response = await service.PatchJobAsync(
            job.Id.ToString(),
            new PatchJobRequest { ApprovalStatus = "Approved" },
            Guid.NewGuid(),
            new[] { "SystemAdmin" });

        response.StatusCode.Should().Be(200);
        response.Data!.ApprovalStatus.Should().Be("Approved");
    }

    // T-OWN-021: approving a job whose department has no head is a 422 business state.
    [Fact]
    public async Task JobApproval_WhenDepartmentHasNoHead_Returns422()
    {
        (JobService service, Job job) = CreateJobServiceWithJob(headUserId: null);

        var response = await service.PatchJobAsync(
            job.Id.ToString(),
            new PatchJobRequest { ApprovalStatus = "Approved" },
            Guid.NewGuid(),
            new[] { "Manager" });

        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be("DEPARTMENT_HEAD_REQUIRED");
        job.Status.Should().Be(JobStatus.PendingApproval);
    }

    // T-OWN-022: approving records the acting head as ApprovedBy (decision-actor audit).
    [Fact]
    public async Task ApprovedJob_SetsApprovedByToCurrentDepartmentHead()
    {
        Guid headId = Guid.NewGuid();
        (JobService service, Job job) = CreateJobServiceWithJob(headId);

        await service.PatchJobAsync(
            job.Id.ToString(),
            new PatchJobRequest { ApprovalStatus = "Approved" },
            headId,
            new[] { "Manager" });

        job.Status.Should().Be(JobStatus.Approved);
        job.ApprovedBy.Should().Be(headId);
    }

    // ---- ManagerReview / DepartmentHeadReview guard (T-OWN-023..027) ----

    // T-OWN-023: HR moving Applied -> Screening is unaffected by the head guard.
    [Fact]
    public async Task HrCanMoveAppliedToScreening()
    {
        var application = BuildApplication(ApplicationStatus.Applied, assignedHeadId: Guid.NewGuid());
        ApplicationService service = CreateApplicationService(application);

        var response = await service.UpdateApplicationDecisionAsync(
            application.Id.ToString(), Guid.NewGuid(), new UpdateApplicationDecisionRequest { TargetStatus = "Screening" });

        response.StatusCode.Should().Be(200);
        application.Status.Should().Be(ApplicationStatus.Screening);
    }

    // T-OWN-024: HR moving Screening -> ManagerReview is unaffected by the head guard.
    [Fact]
    public async Task HrCanMoveScreeningToManagerReview()
    {
        var application = BuildApplication(ApplicationStatus.Screening, assignedHeadId: Guid.NewGuid());
        ApplicationService service = CreateApplicationService(application);

        var response = await service.UpdateApplicationDecisionAsync(
            application.Id.ToString(), Guid.NewGuid(), new UpdateApplicationDecisionRequest { TargetStatus = "ManagerReview" });

        response.StatusCode.Should().Be(200);
        application.Status.Should().Be(ApplicationStatus.ManagerReview);
    }

    // T-OWN-025: the assigned department head can advance a ManagerReview application to Interview.
    [Fact]
    public async Task AssignedDepartmentHeadCanMoveManagerReviewToInterview()
    {
        Guid headId = Guid.NewGuid();
        var application = BuildApplication(ApplicationStatus.ManagerReview, assignedHeadId: headId);
        ApplicationService service = CreateApplicationService(application);

        var response = await service.UpdateApplicationDecisionAsync(
            application.Id.ToString(), headId, new UpdateApplicationDecisionRequest { TargetStatus = "Interview" });

        response.StatusCode.Should().Be(200);
        application.Status.Should().Be(ApplicationStatus.Interview);
    }

    // T-OWN-026: a user who is neither the assigned head nor a SystemAdmin cannot advance ManagerReview.
    [Fact]
    public async Task NonAssignedHeadCannotMoveManagerReviewToInterview()
    {
        Guid headId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();
        var application = BuildApplication(ApplicationStatus.ManagerReview, assignedHeadId: headId);
        ApplicationService service = CreateApplicationService(application, (otherUserId, new[] { "HR" }));

        var response = await service.UpdateApplicationDecisionAsync(
            application.Id.ToString(), otherUserId, new UpdateApplicationDecisionRequest { TargetStatus = "Interview" });

        response.StatusCode.Should().Be(403);
        response.ErrorCode.Should().Be("FORBIDDEN");
        application.Status.Should().Be(ApplicationStatus.ManagerReview);
    }

    // T-OWN-027: the Manager-role fallback applies ONLY when no head was snapshotted.
    [Fact]
    public async Task ManagerReviewGuard_FallsBackOnlyWhenAssignedHeadMissing()
    {
        Guid managerId = Guid.NewGuid();

        // (a) No assigned head -> a Manager-role user may act (temporary migration fallback).
        var noHeadApplication = BuildApplication(ApplicationStatus.ManagerReview, assignedHeadId: null);
        ApplicationService allowed = CreateApplicationService(noHeadApplication, (managerId, new[] { "Manager" }));
        var allowedResponse = await allowed.UpdateApplicationDecisionAsync(
            noHeadApplication.Id.ToString(), managerId, new UpdateApplicationDecisionRequest { TargetStatus = "Interview" });
        allowedResponse.StatusCode.Should().Be(200);

        // (b) An assigned head exists -> a Manager-role non-head is forbidden (no fallback).
        Guid headId = Guid.NewGuid();
        var headApplication = BuildApplication(ApplicationStatus.ManagerReview, assignedHeadId: headId);
        ApplicationService forbidden = CreateApplicationService(headApplication, (managerId, new[] { "Manager" }));
        var forbiddenResponse = await forbidden.UpdateApplicationDecisionAsync(
            headApplication.Id.ToString(), managerId, new UpdateApplicationDecisionRequest { TargetStatus = "Interview" });
        forbiddenResponse.StatusCode.Should().Be(403);
    }

    // ---- helpers ----

    private static (JobService Service, Job Job) CreateJobServiceWithJob(Guid? headUserId)
    {
        Guid jobId = Guid.NewGuid();
        Guid departmentId = Guid.NewGuid();
        var job = new Job
        {
            Id = jobId,
            Title = "Backend Engineer",
            Location = "HCMC",
            Description = "Build the platform.",
            Status = JobStatus.PendingApproval,
            WorkMode = WorkMode.Remote,
            EmploymentType = EmploymentType.FullTime,
            DepartmentId = departmentId,
            Department = new Department { Id = departmentId, Name = "Engineering", HeadUserId = headUserId }
        };

        var jobRepository = new Mock<IJobRepository>();
        jobRepository.Setup(repository => repository.GetTrackedByIdAsync(jobId)).ReturnsAsync(job);

        var service = new JobService(
            jobRepository.Object,
            Mock.Of<IApplicationRepository>(),
            Mock.Of<ISkillRepository>(),
            Mock.Of<IUserRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ISemanticDiscoveryService>(),
            Mock.Of<IMapper>());

        return (service, job);
    }

    private static Domain.Entities.Application BuildApplication(ApplicationStatus status, Guid? assignedHeadId)
    {
        Guid applicationId = Guid.NewGuid();
        Guid candidateId = Guid.NewGuid();
        Guid jobId = Guid.NewGuid();

        return new Domain.Entities.Application
        {
            Id = applicationId,
            UserId = candidateId,
            JobId = jobId,
            Status = status,
            AssignedRecruiterId = Guid.NewGuid(),
            AssignedDepartmentHeadId = assignedHeadId,
            AppliedAt = new DateTime(2026, 6, 1),
            User = new User { Id = candidateId, Username = "candidate", FullName = "Candidate", Email = "c@test.com" },
            Job = new Job
            {
                Id = jobId,
                Title = "Backend Engineer",
                Location = "HCMC",
                Description = "Build the platform.",
                Status = JobStatus.Approved
            }
        };
    }

    private static ApplicationService CreateApplicationService(
        Domain.Entities.Application application,
        (Guid UserId, string[] Roles)? reviewer = null)
    {
        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetTrackedByIdAsync(application.Id)).ReturnsAsync(application);
        applicationRepository.Setup(repository => repository.GetByIdAsync(application.Id)).ReturnsAsync(application);

        var userRepository = new Mock<IUserRepository>();
        if (reviewer.HasValue)
        {
            User user = BuildUserWithRoles(reviewer.Value.UserId, reviewer.Value.Roles);
            userRepository.Setup(repository => repository.GetByIdAsync(reviewer.Value.UserId)).ReturnsAsync(user);
        }

        return new ApplicationService(
            applicationRepository.Object,
            Mock.Of<ICandidateProfileRepository>(),
            userRepository.Object,
            Mock.Of<IJobRepository>(),
            Mock.Of<IOfferRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<IApplicationSemanticProcessingQueue>(),
            Mock.Of<INotificationEventService>(),
            Mock.Of<ILogger<ApplicationService>>());
    }

    private static User BuildUserWithRoles(Guid userId, params string[] roleNames)
    {
        var user = new User { Id = userId, Username = "actor", FullName = "Actor", Email = "actor@test.com" };
        foreach (string roleName in roleNames)
        {
            user.UserRoles.Add(new UserRole { UserId = userId, Role = new Role { Id = Guid.NewGuid(), Name = roleName } });
        }

        return user;
    }
}
