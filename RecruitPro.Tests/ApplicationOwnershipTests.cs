using Microsoft.Extensions.Logging;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Services;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Tests;

/// <summary>
/// Phase 1 — recruitment ownership data foundation.
/// Covers the apply-time owner snapshot (BR-OWN-005) and its fallbacks:
///   AssignedRecruiterId      = Job.RecruiterId ?? Job.CreatedBy
///   AssignedDepartmentHeadId = Job.Department.HeadUserId ?? Job.ApprovedBy
/// See docs/source-of-truth/APPLICATION-OWNERSHIP-FLOW.md and TEST-MATRIX.md (T-OWN-007).
/// </summary>
public sealed class ApplicationOwnershipTests
{
    private static Guid UserId { get; } = Guid.NewGuid();
    private static Guid JobId { get; } = Guid.NewGuid();
    private static Guid CreatedById { get; } = Guid.NewGuid();
    private static Guid RecruiterId { get; } = Guid.NewGuid();
    private static Guid ApprovedById { get; } = Guid.NewGuid();
    private static Guid DepartmentHeadId { get; } = Guid.NewGuid();

    // T-OWN-007: apply snapshots the recruiter from Job.RecruiterId and the department head from
    // Department.HeadUserId.
    [Fact]
    public async Task ApplyAsync_SnapshotsRecruiterAndDepartmentHead_FromPrimarySources()
    {
        Job job = BuildApprovedJob(
            createdBy: CreatedById,
            recruiterId: RecruiterId,
            approvedBy: ApprovedById,
            departmentHeadId: DepartmentHeadId);

        Domain.Entities.Application? captured = null;
        ApplicationService service = CreateService(job, application => captured = application);

        var response = await service.ApplyAsync(UserId, JobId.ToString(), new ApplyJobRequest());

        response.StatusCode.Should().Be(201);
        captured.Should().NotBeNull();
        captured!.AssignedRecruiterId.Should().Be(RecruiterId);
        captured.AssignedDepartmentHeadId.Should().Be(DepartmentHeadId);
    }

    // T-OWN-007 (recruiter fallback): with no Job.RecruiterId, AssignedRecruiterId falls back to the
    // audit Job.CreatedBy.
    [Fact]
    public async Task ApplyAsync_RecruiterFallsBackToCreatedBy_WhenRecruiterIdMissing()
    {
        Job job = BuildApprovedJob(
            createdBy: CreatedById,
            recruiterId: null,
            approvedBy: ApprovedById,
            departmentHeadId: DepartmentHeadId);

        Domain.Entities.Application? captured = null;
        ApplicationService service = CreateService(job, application => captured = application);

        var response = await service.ApplyAsync(UserId, JobId.ToString(), new ApplyJobRequest());

        response.StatusCode.Should().Be(201);
        captured!.AssignedRecruiterId.Should().Be(CreatedById);
    }

    // T-OWN-007 (head fallback): the audit Job.ApprovedBy is used for the department head ONLY when
    // Department.HeadUserId is missing.
    [Fact]
    public async Task ApplyAsync_DepartmentHeadFallsBackToApprovedBy_OnlyWhenHeadUserMissing()
    {
        Job job = BuildApprovedJob(
            createdBy: CreatedById,
            recruiterId: RecruiterId,
            approvedBy: ApprovedById,
            departmentHeadId: null);

        Domain.Entities.Application? captured = null;
        ApplicationService service = CreateService(job, application => captured = application);

        var response = await service.ApplyAsync(UserId, JobId.ToString(), new ApplyJobRequest());

        response.StatusCode.Should().Be(201);
        captured!.AssignedDepartmentHeadId.Should().Be(ApprovedById);
    }

    // The department head is preferred over the approver when both exist (head is not overridden by the
    // audit fallback).
    [Fact]
    public async Task ApplyAsync_PrefersDepartmentHead_OverApprovedBy_WhenBothPresent()
    {
        Job job = BuildApprovedJob(
            createdBy: CreatedById,
            recruiterId: RecruiterId,
            approvedBy: ApprovedById,
            departmentHeadId: DepartmentHeadId);

        Domain.Entities.Application? captured = null;
        ApplicationService service = CreateService(job, application => captured = application);

        await service.ApplyAsync(UserId, JobId.ToString(), new ApplyJobRequest());

        captured!.AssignedDepartmentHeadId.Should().Be(DepartmentHeadId);
        captured.AssignedDepartmentHeadId.Should().NotBe(ApprovedById);
    }

    private static Job BuildApprovedJob(Guid createdBy, Guid? recruiterId, Guid? approvedBy, Guid? departmentHeadId)
    {
        return new Job
        {
            Id = JobId,
            Title = "Backend Engineer",
            Location = "HCMC",
            Description = "Build the platform.",
            Status = JobStatus.Approved,
            WorkMode = WorkMode.Remote,
            EmploymentType = EmploymentType.FullTime,
            Deadline = null,
            CreatedBy = createdBy,
            RecruiterId = recruiterId,
            ApprovedBy = approvedBy,
            Department = departmentHeadId.HasValue
                ? new Department { Id = Guid.NewGuid(), Name = "Engineering", HeadUserId = departmentHeadId }
                : null
        };
    }

    private static User BuildUser()
    {
        return new User
        {
            Id = UserId,
            Username = "candidate.user",
            FullName = "Candidate User",
            Email = "candidate@test.com"
        };
    }

    private static CandidateProfile BuildProfileWithResume()
    {
        return new CandidateProfile
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            User = BuildUser(),
            ResumeUrl = "resumes/candidate-cv.pdf"
        };
    }

    private static ApplicationService CreateService(Job job, Action<Domain.Entities.Application> onAdd)
    {
        var candidateRepository = new Mock<ICandidateProfileRepository>();
        candidateRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync(BuildProfileWithResume());

        var jobRepository = new Mock<IJobRepository>();
        jobRepository.Setup(repository => repository.GetByIdAsync(JobId)).ReturnsAsync(job);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync([]);
        applicationRepository.Setup(repository => repository.HasActiveApplicationAsync(UserId, JobId)).ReturnsAsync(false);
        applicationRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Domain.Entities.Application>()))
            .Callback<Domain.Entities.Application>(application => onAdd(application))
            .Returns(Task.CompletedTask);

        return new ApplicationService(
            applicationRepository.Object,
            candidateRepository.Object,
            Mock.Of<IUserRepository>(),
            jobRepository.Object,
            Mock.Of<IOfferRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<IApplicationSemanticProcessingQueue>(),
            Mock.Of<INotificationEventService>(),
            Mock.Of<IEmailService>(),
            Mock.Of<ILogger<ApplicationService>>());
    }
}
