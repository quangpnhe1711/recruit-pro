using AutoMapper;
using Microsoft.Extensions.Logging;
using RecruitPro.Application.DTOs.Request.Interviews;
using RecruitPro.Application.DTOs.Request.Jobs;
using RecruitPro.Application.Interfaces;
using RecruitPro.Application.Interfaces.IRepositories;
using RecruitPro.Application.Interfaces.IServices;
using RecruitPro.Application.Services;
using RecruitPro.Domain.Entities;
using RecruitPro.Domain.Enums;

namespace RecruitPro.Tests;

/// <summary>
/// Conformance coverage for the state-dependency invariants (docs/source-of-truth):
/// INV-003 (EXISTS-active duplicate), INV-008 (interview gated by Application.status),
/// INV-009 (Hired only from candidate accept), INV-015 (Hired terminal for jobId).
/// </summary>
public sealed class ApplicationConformanceTests
{
    private static Guid UserId { get; } = Guid.NewGuid();
    private static Guid JobId { get; } = Guid.NewGuid();

    // T-RE-003 / INV-015: a prior Hired application for the SAME job blocks re-apply with 422
    // (terminal for jobId), NOT a 409 active-duplicate, and inserts nothing.
    [Fact]
    public async Task ApplyAsync_AfterHired_ForSameJob_IsBlocked()
    {
        var hiredApplication = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            JobId = JobId,
            Status = ApplicationStatus.Hired,
            AppliedAt = new DateTime(2026, 1, 1)
        };

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync([hiredApplication]);
        applicationRepository.Setup(repository => repository.HasActiveApplicationAsync(UserId, JobId)).ReturnsAsync(false);

        ApplicationService service = CreateService(applicationRepository: applicationRepository.Object);

        var response = await service.ApplyAsync(UserId, JobId.ToString(), new ApplyJobRequest());

        response.Success.Should().BeFalse();
        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be("APPLICATION_ALREADY_HIRED");
        response.Message.Should().NotBe("Candidate already applied for this job.");
        applicationRepository.Verify(repository => repository.AddAsync(It.IsAny<Domain.Entities.Application>()), Times.Never);
    }

    // T-DUP-003 / INV-003: the duplicate decision is the EXISTS-active query and is independent of
    // which row GetByUserIdAsync happens to return first. Here the only fetched row is a CLOSED
    // (withdrawn) row, yet HasActiveApplicationAsync reports an active application exists (e.g. a
    // second, concurrently-created row) — apply must still be blocked with 409, never insert.
    [Fact]
    public async Task ApplyAsync_UsesExistsActive_NotArbitraryRow()
    {
        var withdrawnRow = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            JobId = JobId,
            Status = ApplicationStatus.Withdrawn,
            AppliedAt = new DateTime(2026, 1, 1)
        };

        var applicationRepository = new Mock<IApplicationRepository>();
        // Fetched list contains ONLY a closed row — a naive "is the fetched row active?" check would
        // wrongly allow the apply. The EXISTS-active query is the source of truth.
        applicationRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync([withdrawnRow]);
        applicationRepository.Setup(repository => repository.HasActiveApplicationAsync(UserId, JobId)).ReturnsAsync(true);

        ApplicationService service = CreateService(applicationRepository: applicationRepository.Object);

        var response = await service.ApplyAsync(UserId, JobId.ToString(), new ApplyJobRequest());

        response.StatusCode.Should().Be(409);
        response.ErrorCode.Should().Be("APPLICATION_ALREADY_ACTIVE");
        applicationRepository.Verify(repository => repository.AddAsync(It.IsAny<Domain.Entities.Application>()), Times.Never);
    }

    // T-OFR-002 / INV-009: candidate accepting a Sent offer while in Offer sets Offer=Accepted and
    // Application=Hired.
    [Fact]
    public async Task AcceptOffer_WhenOfferSent_SetsOfferAcceptedAndApplicationHired()
    {
        Guid applicationId = Guid.NewGuid();
        var application = BuildCandidateApplication(applicationId, ApplicationStatus.Offer);
        var offer = new ApplicationOffer { ApplicationId = applicationId, Status = OfferStatus.Sent };

        var (applicationRepository, offerRepository) = BuildOfferMocks(applicationId, application, offer);

        ApplicationService service = CreateService(
            applicationRepository: applicationRepository.Object,
            offerRepository: offerRepository.Object);

        var response = await service.AcceptOfferAsync(UserId, applicationId.ToString());

        response.StatusCode.Should().Be(200);
        application.Status.Should().Be(ApplicationStatus.Hired);
        offer.Status.Should().Be(OfferStatus.Accepted);
    }

    // T-OFR-003 / INV-009: candidate declining a Sent offer sets Offer=Declined and
    // Application=OfferDeclined.
    [Fact]
    public async Task DeclineOffer_WhenOfferSent_SetsOfferDeclinedAndApplicationOfferDeclined()
    {
        Guid applicationId = Guid.NewGuid();
        var application = BuildCandidateApplication(applicationId, ApplicationStatus.Offer);
        var offer = new ApplicationOffer { ApplicationId = applicationId, Status = OfferStatus.Sent };

        var (applicationRepository, offerRepository) = BuildOfferMocks(applicationId, application, offer);

        ApplicationService service = CreateService(
            applicationRepository: applicationRepository.Object,
            offerRepository: offerRepository.Object);

        var response = await service.DeclineOfferAsync(UserId, applicationId.ToString());

        response.StatusCode.Should().Be(200);
        application.Status.Should().Be(ApplicationStatus.OfferDeclined);
        offer.Status.Should().Be(OfferStatus.Declined);
    }

    // T-OFR-001 / INV-009: forbidden combinations are rejected as 422 (business state), not 400/500.
    [Fact]
    public async Task AcceptOffer_WhenNotInOfferStage_Returns422()
    {
        Guid applicationId = Guid.NewGuid();
        var application = BuildCandidateApplication(applicationId, ApplicationStatus.Screening);
        var (applicationRepository, offerRepository) = BuildOfferMocks(applicationId, application, offer: null);

        ApplicationService service = CreateService(
            applicationRepository: applicationRepository.Object,
            offerRepository: offerRepository.Object);

        var response = await service.AcceptOfferAsync(UserId, applicationId.ToString());

        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be("OFFER_NOT_ACTIONABLE");
        application.Status.Should().Be(ApplicationStatus.Screening);
    }

    [Fact]
    public async Task AcceptOffer_WhenOfferNotSent_Returns422()
    {
        Guid applicationId = Guid.NewGuid();
        var application = BuildCandidateApplication(applicationId, ApplicationStatus.Offer);
        var draftOffer = new ApplicationOffer { ApplicationId = applicationId, Status = OfferStatus.Draft };
        var (applicationRepository, offerRepository) = BuildOfferMocks(applicationId, application, draftOffer);

        ApplicationService service = CreateService(
            applicationRepository: applicationRepository.Object,
            offerRepository: offerRepository.Object);

        var response = await service.AcceptOfferAsync(UserId, applicationId.ToString());

        response.StatusCode.Should().Be(422);
        application.Status.Should().Be(ApplicationStatus.Offer);
    }

    // T-OFR-001 / INV-009: a reviewer (HR/Manager) must NOT drive the application out of Offer via the
    // decision endpoint — Hired/OfferDeclined are candidate-owned outcomes.
    [Fact]
    public async Task UpdateApplicationDecision_FromOffer_IsRejected()
    {
        Guid applicationId = Guid.NewGuid();
        var application = BuildCandidateApplication(applicationId, ApplicationStatus.Offer);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetTrackedByIdAsync(applicationId)).ReturnsAsync(application);

        ApplicationService service = CreateService(applicationRepository: applicationRepository.Object);

        var response = await service.UpdateApplicationDecisionAsync(
            applicationId.ToString(),
            Guid.NewGuid(),
            new RecruitPro.Application.DTOs.Request.Applications.UpdateApplicationDecisionRequest { TargetStatus = "Hired" });

        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be("INVALID_APPLICATION_TRANSITION");
        application.Status.Should().Be(ApplicationStatus.Offer);
    }

    // T-INT-002 / INV-008: withdrawing from the Interview stage cancels the pending (Scheduled)
    // interview so it is no longer actionable.
    [Fact]
    public async Task Withdraw_FromInterviewStage_CancelsPendingInterview()
    {
        Guid applicationId = Guid.NewGuid();
        var application = BuildCandidateApplication(applicationId, ApplicationStatus.Interview);
        var scheduled = new Interview { Id = Guid.NewGuid(), ApplicationId = applicationId, Status = InterviewStatus.Scheduled };
        var completed = new Interview { Id = Guid.NewGuid(), ApplicationId = applicationId, Status = InterviewStatus.Completed };
        application.Interviews.Add(scheduled);
        application.Interviews.Add(completed);

        var candidateRepository = new Mock<ICandidateProfileRepository>();
        candidateRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync(BuildProfile());

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetTrackedByIdAsync(applicationId)).ReturnsAsync(application);

        ApplicationService service = CreateService(
            applicationRepository: applicationRepository.Object,
            candidateRepository: candidateRepository.Object);

        var response = await service.WithdrawApplicationAsync(UserId, applicationId.ToString());

        response.StatusCode.Should().Be(200);
        application.Status.Should().Be(ApplicationStatus.Withdrawn);
        scheduled.Status.Should().Be(InterviewStatus.Canceled);
        // A completed interview is history and must be left untouched.
        completed.Status.Should().Be(InterviewStatus.Completed);
    }

    // T-INT-001 / INV-008: scheduling an interview while the application is not at the ManagerReview/
    // Interview stage is an invalid business state (422), not a malformed request.
    [Fact]
    public async Task CreateInterview_WhenApplicationNotInInterviewStage_Returns422()
    {
        Guid applicationId = Guid.NewGuid();
        var application = BuildCandidateApplication(applicationId, ApplicationStatus.Applied);

        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetTrackedByIdAsync(applicationId)).ReturnsAsync(application);

        var service = new InterviewService(
            Mock.Of<IInterviewRepository>(),
            applicationRepository.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<INotificationEventService>(),
            CreateMapper());

        var response = await service.CreateInterviewAsync(new CreateInterviewRequest
        {
            ApplicationId = applicationId.ToString(),
            Mode = "video",
            Status = "scheduled"
        });

        response.StatusCode.Should().Be(422);
        response.ErrorCode.Should().Be("INTERVIEW_NOT_ACTIONABLE");
    }

    private static (Mock<IApplicationRepository>, Mock<IOfferRepository>) BuildOfferMocks(
        Guid applicationId,
        Domain.Entities.Application application,
        ApplicationOffer? offer)
    {
        var applicationRepository = new Mock<IApplicationRepository>();
        applicationRepository.Setup(repository => repository.GetTrackedByIdAsync(applicationId)).ReturnsAsync(application);

        var offerRepository = new Mock<IOfferRepository>();
        offerRepository.Setup(repository => repository.GetTrackedByApplicationIdAsync(applicationId)).ReturnsAsync(offer);

        return (applicationRepository, offerRepository);
    }

    private static Domain.Entities.Application BuildCandidateApplication(Guid applicationId, ApplicationStatus status)
    {
        return new Domain.Entities.Application
        {
            Id = applicationId,
            UserId = UserId,
            JobId = JobId,
            Status = status,
            User = BuildUser(),
            Job = BuildApprovedJob()
        };
    }

    private static User BuildUser()
    {
        return new User { Id = UserId, Username = "candidate.user", FullName = "Candidate User", Email = "candidate@test.com" };
    }

    private static CandidateProfile BuildProfile()
    {
        return new CandidateProfile { Id = Guid.NewGuid(), UserId = UserId, User = BuildUser(), ResumeUrl = "resumes/cv.pdf" };
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
            EmploymentType = EmploymentType.FullTime
        };
    }

    private static IMapper CreateMapper()
    {
        // Minimal mapper; the interview tests exercised here return before any mapping occurs.
        return new MapperConfiguration(_ => { }).CreateMapper();
    }

    private static ApplicationService CreateService(
        IApplicationRepository? applicationRepository = null,
        ICandidateProfileRepository? candidateRepository = null,
        IOfferRepository? offerRepository = null)
    {
        if (candidateRepository == null)
        {
            var defaultCandidateRepository = new Mock<ICandidateProfileRepository>();
            defaultCandidateRepository.Setup(repository => repository.GetByUserIdAsync(UserId)).ReturnsAsync(BuildProfile());
            candidateRepository = defaultCandidateRepository.Object;
        }

        var jobRepository = new Mock<IJobRepository>();
        jobRepository.Setup(repository => repository.GetByIdAsync(JobId)).ReturnsAsync(BuildApprovedJob());

        return new ApplicationService(
            applicationRepository ?? Mock.Of<IApplicationRepository>(),
            candidateRepository,
            Mock.Of<IUserRepository>(),
            jobRepository.Object,
            offerRepository ?? Mock.Of<IOfferRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<IApplicationSemanticProcessingQueue>(),
            Mock.Of<INotificationEventService>(),
            Mock.Of<IEmailService>(),
            Mock.Of<ILogger<ApplicationService>>());
    }
}
