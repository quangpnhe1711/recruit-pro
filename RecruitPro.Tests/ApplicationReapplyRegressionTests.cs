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
/// Regression coverage for the two reported production defects:
///   * BUG-APPLICATION-001 — withdraw then re-apply was blocked with "Candidate already applied".
///   * BUG-APPLICATION-002 — a normal, valid apply could surface HTTP 500.
/// See docs/source-of-truth/BUSINESS-RULES.md (BR-APPLICATION-001/002) and ERROR-CONTRACT.md.
/// </summary>
public sealed class ApplicationReapplyRegressionTests
{
    private static Guid UserId { get; } = Guid.NewGuid();
    private static Guid JobId { get; } = Guid.NewGuid();

    // TEST-APPLICATION-WITHDRAW-001: withdraw must record a dedicated Withdrawn state, never Rejected.
    [Fact]
    public async Task WithdrawApplicationAsync_WhenAllowed_SetsStatusToWithdrawnNotRejected()
    {
        Guid applicationId = Guid.NewGuid();
        var application = new Domain.Entities.Application
        {
            Id = applicationId,
            UserId = UserId,
            JobId = JobId,
            Status = ApplicationStatus.Applied,
            User = BuildUser(),
            Job = BuildApprovedJob()
        };

        var candidateRepository = new Mock<ICandidateProfileRepository>();
        candidateRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync(BuildProfileWithResume());

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetTrackedByIdAsync(applicationId)).ReturnsAsync(application);

        ApplicationService service = CreateService(
            applicationRepository: applicationRepository.Object,
            candidateRepository: candidateRepository.Object);

        var response = await service.WithdrawApplicationAsync(UserId, applicationId.ToString());

        response.Success.Should().BeTrue();
        response.StatusCode.Should().Be(200);
        application.Status.Should().Be(ApplicationStatus.Withdrawn);
        application.Status.Should().NotBe(ApplicationStatus.Rejected);
        applicationRepository.Verify(repository => repository.UpdateAsync(application), Times.Once);
    }

    // TEST-APPLICATION-WITHDRAW-002: withdrawing an application that is past the withdraw window is a
    // business-state error (422), not an unhandled failure.
    [Fact]
    public async Task WithdrawApplicationAsync_WhenAlreadyClosed_Returns422()
    {
        Guid applicationId = Guid.NewGuid();
        var application = new Domain.Entities.Application
        {
            Id = applicationId,
            UserId = UserId,
            JobId = JobId,
            Status = ApplicationStatus.Hired,
            User = BuildUser(),
            Job = BuildApprovedJob()
        };

        var candidateRepository = new Mock<ICandidateProfileRepository>();
        candidateRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync(BuildProfileWithResume());

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetTrackedByIdAsync(applicationId)).ReturnsAsync(application);

        ApplicationService service = CreateService(
            applicationRepository: applicationRepository.Object,
            candidateRepository: candidateRepository.Object);

        var response = await service.WithdrawApplicationAsync(UserId, applicationId.ToString());

        response.StatusCode.Should().Be(422);
        application.Status.Should().Be(ApplicationStatus.Hired);
    }

    // TEST-APPLICATION-REAPPLY-001: the headline bug. A withdrawn (closed) application must NOT block
    // a fresh application — re-apply is allowed and returns 201 Created.
    [Fact]
    public async Task ApplyAsync_AfterWithdrawal_AllowsReapplyAndReturnsCreated()
    {
        var withdrawnApplication = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            JobId = JobId,
            Status = ApplicationStatus.Withdrawn,
            AppliedAt = new DateTime(2026, 1, 1)
        };

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync([withdrawnApplication]);

        ApplicationService service = CreateService(applicationRepository: applicationRepository.Object);

        var response = await service.ApplyAsync(UserId, JobId.ToString(), new ApplyJobRequest());

        response.Success.Should().BeTrue();
        response.StatusCode.Should().Be(201);
        applicationRepository.Verify(repository => repository.AddAsync(It.IsAny<Domain.Entities.Application>()), Times.Once);
    }

    // TEST-APPLICATION-REAPPLY-002: re-apply after an HR rejection is also allowed (Rejected is closed).
    [Fact]
    public async Task ApplyAsync_AfterRejection_AllowsReapplyAndReturnsCreated()
    {
        var rejectedApplication = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            JobId = JobId,
            Status = ApplicationStatus.Rejected,
            AppliedAt = new DateTime(2026, 1, 1)
        };

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync([rejectedApplication]);

        ApplicationService service = CreateService(applicationRepository: applicationRepository.Object);

        var response = await service.ApplyAsync(UserId, JobId.ToString(), new ApplyJobRequest());

        response.StatusCode.Should().Be(201);
    }

    // TEST-APPLICATION-DUPLICATE-001: an ACTIVE application is a genuine duplicate — 409 Conflict.
    [Theory]
    [InlineData(ApplicationStatus.Applied)]
    [InlineData(ApplicationStatus.Screening)]
    [InlineData(ApplicationStatus.ManagerReview)]
    [InlineData(ApplicationStatus.Interview)]
    [InlineData(ApplicationStatus.Offer)]
    public async Task ApplyAsync_WhenActiveApplicationExists_Returns409Conflict(ApplicationStatus activeStatus)
    {
        var activeApplication = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            JobId = JobId,
            Status = activeStatus,
            AppliedAt = new DateTime(2026, 1, 1)
        };

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync([activeApplication]);
        // INV-003: the duplicate decision is the EXISTS-active query, not the fetched row.
        applicationRepository.Setup(repository => repository.HasActiveApplicationAsync(UserId, JobId)).ReturnsAsync(true);

        ApplicationService service = CreateService(applicationRepository: applicationRepository.Object);

        var response = await service.ApplyAsync(UserId, JobId.ToString(), new ApplyJobRequest());

        response.Success.Should().BeFalse();
        response.StatusCode.Should().Be(409);
        response.Message.Should().Be("Candidate already applied for this job.");
        response.ErrorCode.Should().Be("APPLICATION_ALREADY_ACTIVE");
        applicationRepository.Verify(repository => repository.AddAsync(It.IsAny<Domain.Entities.Application>()), Times.Never);
    }

    // TEST-APPLICATION-MESSAGE-001: when re-apply is blocked by a DIFFERENT reason (e.g. job not open),
    // the response must report that reason (422) and must NOT falsely claim "already applied".
    [Fact]
    public async Task ApplyAsync_WhenBlockedByNonDuplicateReason_DoesNotClaimAlreadyApplied()
    {
        var withdrawnApplication = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            JobId = JobId,
            Status = ApplicationStatus.Withdrawn,
            AppliedAt = new DateTime(2026, 1, 1)
        };

        Job closedJob = BuildApprovedJob();
        closedJob.Status = JobStatus.Closed;

        var jobRepository = new Mock<IJobRepository>();
        jobRepository.Setup(repository => repository.GetByIdAsync(JobId)).ReturnsAsync(closedJob);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync([withdrawnApplication]);

        ApplicationService service = CreateService(
            applicationRepository: applicationRepository.Object,
            jobRepository: jobRepository.Object);

        var response = await service.ApplyAsync(UserId, JobId.ToString(), new ApplyJobRequest());

        response.Success.Should().BeFalse();
        response.StatusCode.Should().Be(422);
        response.Message.Should().NotBe("Candidate already applied for this job.");
        response.Message.Should().Contain("not accepting new applications");
    }

    // TEST-APPLICATION-500-001: the apply is durably committed before any side effect runs. A failing
    // notification dispatch must NOT turn a successful apply into HTTP 500.
    [Fact]
    public async Task ApplyAsync_WhenNotificationPublishFails_StillReturnsCreated()
    {
        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync([]);

        var notificationEventService = new Mock<INotificationEventService>();
        notificationEventService
            .Setup(service => service.PublishNewApplicationReceivedAsync(It.IsAny<Domain.Entities.Application>()))
            .ThrowsAsync(new InvalidOperationException("notification backend unavailable"));

        ApplicationService service = CreateService(
            applicationRepository: applicationRepository.Object,
            notificationEventService: notificationEventService.Object);

        var response = await service.ApplyAsync(UserId, JobId.ToString(), new ApplyJobRequest());

        response.Success.Should().BeTrue();
        response.StatusCode.Should().Be(201);
        applicationRepository.Verify(repository => repository.AddAsync(It.IsAny<Domain.Entities.Application>()), Times.Once);
    }

    // TEST-APPLICATION-500-002: a failing semantic-scoring enqueue is also a best-effort side effect.
    [Fact]
    public async Task ApplyAsync_WhenSemanticEnqueueFails_StillReturnsCreated()
    {
        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync([]);

        var queue = new Mock<IApplicationSemanticProcessingQueue>();
        queue.Setup(value => value.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue offline"));

        ApplicationService service = CreateService(
            applicationRepository: applicationRepository.Object,
            semanticProcessingQueue: queue.Object);

        var response = await service.ApplyAsync(UserId, JobId.ToString(), new ApplyJobRequest());

        response.StatusCode.Should().Be(201);
    }

    // TEST-APPLICATION-CONTEXT-001: apply-context after withdrawal lets the candidate re-apply and no
    // longer reports an active application — this is what re-enables the FE Apply button.
    [Fact]
    public async Task GetApplyScreenAsync_AfterWithdrawal_ReportsCanApplyAndNotAlreadyApplied()
    {
        var withdrawnApplication = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            JobId = JobId,
            Status = ApplicationStatus.Withdrawn,
            AppliedAt = new DateTime(2026, 1, 1)
        };

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync([withdrawnApplication]);

        ApplicationService service = CreateService(applicationRepository: applicationRepository.Object);

        var response = await service.GetApplyScreenAsync(UserId, JobId.ToString());

        response.Success.Should().BeTrue();
        response.Data!.Eligibility.CanApply.Should().BeTrue();
        response.Data.Eligibility.AlreadyApplied.Should().BeFalse();
        response.Data.Eligibility.ExistingApplicationStatus.Should().Be("Withdrawn");
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
            // A current resume is required to pass apply eligibility; the URL fallback is the simplest.
            ResumeUrl = "resumes/candidate-cv.pdf"
        };
    }

    private static Job BuildApprovedJob()
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
            Deadline = null
        };
    }

    private static ApplicationService CreateService(
        IApplicationRepository? applicationRepository = null,
        ICandidateProfileRepository? candidateRepository = null,
        IJobRepository? jobRepository = null,
        INotificationEventService? notificationEventService = null,
        IApplicationSemanticProcessingQueue? semanticProcessingQueue = null)
    {
        if (candidateRepository == null)
        {
            var defaultCandidateRepository = new Mock<ICandidateProfileRepository>();
            defaultCandidateRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync(BuildProfileWithResume());
            candidateRepository = defaultCandidateRepository.Object;
        }

        if (jobRepository == null)
        {
            var defaultJobRepository = new Mock<IJobRepository>();
            defaultJobRepository.Setup(repository => repository.GetByIdAsync(JobId)).ReturnsAsync(BuildApprovedJob());
            jobRepository = defaultJobRepository.Object;
        }

        return new ApplicationService(
            applicationRepository ?? Mock.Of<IApplicationRepository>(),
            candidateRepository,
            Mock.Of<IUserRepository>(),
            jobRepository,
            Mock.Of<IOfferRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IFileStorageService>(),
            semanticProcessingQueue ?? Mock.Of<IApplicationSemanticProcessingQueue>(),
            notificationEventService ?? Mock.Of<INotificationEventService>(),
            Mock.Of<ILogger<ApplicationService>>());
    }
}
